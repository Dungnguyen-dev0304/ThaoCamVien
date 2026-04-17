using SQLite;
using SharedThaoCamVien.Models;
using System.Net.Http.Json;
using System.Net.Http;
using System.Diagnostics;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// Tầng Content Layer — quản lý dữ liệu SQLite + đồng bộ API.
    /// Chiến lược Offline First:
    ///   1. Thử gọi API → lưu SQLite
    ///   2. Mất mạng → dùng SQLite cũ
    ///   3. SQLite rỗng → seed hardcode (app không bao giờ trắng màn hình)
    /// </summary>
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _db;
        private readonly SemaphoreSlim _dbInitLock = new(1, 1);
        private const string DB_NAME = "tcv_v5.db3";

        private string _apiBase;
        private readonly HttpClient _httpClient;
        private readonly ResiliencePipeline _pipeline;

        // UI có thể đọc để hiển thị message cho người dùng
        public string? LastSyncError { get; private set; }

        public DatabaseService()
        {
            // Dùng chung logic resolve URL với ApiService (không hardcode IP cá nhân).
            var pref = Preferences.Default.Get("ApiBaseUrl", string.Empty);
            var baseUrl = string.IsNullOrWhiteSpace(pref) ? ApiService.ResolveDefaultApiUrl() : pref;
            _apiBase = baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
                ? baseUrl.TrimEnd('/')
                : $"{baseUrl.TrimEnd('/')}/api";

            var handler = new HttpClientHandler();
#if DEBUG
            // Chỉ bypass cert trong dev (nếu API chạy self-signed).
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
            _httpClient = new HttpClient(handler)
            {
                // HttpClient timeout > Polly total timeout (15s)
                Timeout = TimeSpan.FromSeconds(20)
            };

            _pipeline = ResiliencePolicies.HttpPipeline;
        }

        public string CurrentLanguage { get; set; } = "vi";

        /// <summary>
        /// Đọc lại URL từ Preferences (khi user thay đổi IP ở trang cấu hình).
        /// Gọi trước mỗi lần sync để đảm bảo dùng URL mới nhất.
        /// </summary>
        public void RefreshApiBaseUrl()
        {
            var pref = Preferences.Default.Get("ApiBaseUrl", string.Empty);
            var baseUrl = string.IsNullOrWhiteSpace(pref) ? ApiService.ResolveDefaultApiUrl() : pref;
            _apiBase = baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
                ? baseUrl.TrimEnd('/')
                : $"{baseUrl.TrimEnd('/')}/api";
        }

        // ─── KHỞI TẠO DB ─────────────────────────────────────────────────
        private async Task<SQLiteAsyncConnection> GetDbAsync()
        {
            if (_db != null) return _db;

            await _dbInitLock.WaitAsync();
            try
            {
                if (_db != null) return _db;

                var path = Path.Combine(FileSystem.AppDataDirectory, DB_NAME);
                _db = new SQLiteAsyncConnection(path,
                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

                await EnsureSchemaAsync(_db);
                return _db;
            }
            finally
            {
                _dbInitLock.Release();
            }
        }

        private static async Task EnsureSchemaAsync(SQLiteAsyncConnection db)
        {
            await db.CreateTableAsync<Poi>();
            await db.CreateTableAsync<PoiMedium>();
            await db.CreateTableAsync<QrCode>();
            await db.CreateTableAsync<PoiVisitHistory>();
            await db.CreateTableAsync<PoiTranslation>();
        }

        private static bool IsUnknownTypeError(Exception ex)
            => ex.Message.Contains("Don't know about", StringComparison.OrdinalIgnoreCase);

        private async Task<SQLiteAsyncConnection> ReopenDbAsync()
        {
            await _dbInitLock.WaitAsync();
            try
            {
                var path = Path.Combine(FileSystem.AppDataDirectory, DB_NAME);
                _db = new SQLiteAsyncConnection(path,
                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
                await EnsureSchemaAsync(_db);
                return _db;
            }
            finally
            {
                _dbInitLock.Release();
            }
        }

        // ─── ĐỒNG BỘ API ─────────────────────────────────────────────────
        public async Task SyncDataFromApiAsync()
        {
            LastSyncError = null;
            RefreshApiBaseUrl();

            // Không có internet -> vẫn dùng cache/sideseed offline
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                LastSyncError = "Không có Internet/NetworkAccess.";
                await EnsureOfflineDataAsync();
                return;
            }

            try
            {
                var url = $"{_apiBase}/Pois?lang={CurrentLanguage}";
                Debug.WriteLine($"[DB] SyncDataFromApiAsync url={url}");

                // Polly pipeline: Retry(2x) + CircuitBreaker + Timeout(15s)
                var pois = await _pipeline.ExecuteAsync(async ct =>
                {
                    return await _httpClient.GetFromJsonAsync<List<Poi>>(url, ct);
                }, CancellationToken.None);

                if (pois != null && pois.Count > 0)
                {
                    var db = await GetDbAsync();
                    // Dùng InsertOrReplace thay vì Delete+Insert
                    // để giữ nguyên PoiId từ server (không bị SQLite auto-increment gán ID mới)
                    await db.DeleteAllAsync<Poi>();
                    foreach (var poi in pois)
                    {
                        await db.InsertOrReplaceAsync(poi);
                    }
                    Debug.WriteLine($"[DB] API sync OK: {pois.Count} POIs (InsertOrReplace)");
                    return;
                }

                // API phản hồi được nhưng rỗng -> không xóa cache hiện tại
                Debug.WriteLine("[DB] API trả về rỗng/không có active POIs.");
            }
            catch (BrokenCircuitException)
            {
                LastSyncError = "Server không khả dụng (circuit open).";
                Debug.WriteLine("[DB] Circuit breaker open → offline");
            }
            catch (TimeoutRejectedException)
            {
                LastSyncError = "API timeout (Polly).";
                Debug.WriteLine("[DB] Polly total timeout → offline");
            }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                LastSyncError = "API timeout.";
                Debug.WriteLine($"[DB] API timeout: {ex.Message} → offline");
            }
            catch (HttpRequestException ex)
            {
                LastSyncError = "API network error.";
                Debug.WriteLine($"[DB] API network error: {ex.Message} → offline");
            }
            catch (System.Text.Json.JsonException ex)
            {
                LastSyncError = "API JSON parse error.";
                Debug.WriteLine($"[DB] API JSON error: {ex.Message} → offline");
            }
            catch (Exception ex)
            {
                LastSyncError = ex.Message;
                Debug.WriteLine($"[DB] API lỗi: {ex.Message} → offline");
            }

            await EnsureOfflineDataAsync();
        }

        // ─── API-FIRST fallback: nếu SQLite rỗng thì seed offline tối thiểu ─────────────────────
        private async Task EnsureOfflineDataAsync()
        {
            SQLiteAsyncConnection db;
            try
            {
                db = await GetDbAsync();
                if (await db.Table<Poi>().CountAsync() > 0)
                    return;
            }
            catch (Exception ex) when (IsUnknownTypeError(ex))
            {
                Debug.WriteLine($"[DB] Reopen SQLite mapping for EnsureOfflineData: {ex.Message}");
                db = await ReopenDbAsync();
                if (await db.Table<Poi>().CountAsync() > 0)
                    return;
            }

            System.Diagnostics.Debug.WriteLine("[DB] Cache rỗng → seed offline tối thiểu.");
            await SeedOfflinePoisAsync(db);
        }

        private static async Task SeedOfflinePoisAsync(SQLiteAsyncConnection db)
        {
            // Seed 5 POI với đủ nhóm danh mục để UI luôn có bộ lọc tối thiểu khi offline.
            // Dữ liệu sẽ được thay thế khi SyncDataFromApiAsync gọi thành công.
            var seed = new List<Poi>
            {
                new Poi
                {
                    PoiId = 1, CategoryId = 2, Name = "Voi Châu Á",
                    Description = "Voi châu Á là loài voi lớn nhất châu Á, sống chủ yếu trong rừng nhiệt đới.",
                    Latitude = 10.7876m, Longitude = 106.7048m, Radius = 30, Priority = 10,
                    ImageThumbnail = "elephant.jpg", IsActive = true
                },
                new Poi
                {
                    PoiId = 2, CategoryId = 2, Name = "Hổ Bengal",
                    Description = "Hổ Bengal là loài hổ lớn nhất, biểu tượng sức mạnh hoang dã.",
                    Latitude = 10.7880m, Longitude = 106.7052m, Radius = 25, Priority = 9,
                    ImageThumbnail = "tiger.jpg", IsActive = true
                },
                new Poi
                {
                    PoiId = 3, CategoryId = 3, Name = "Nhà hoa lan kiểng",
                    Description = "Khu thực vật với nhiều giống lan và cây kiểng đặc trưng.",
                    Latitude = 10.7882m, Longitude = 106.7060m, Radius = 25, Priority = 8,
                    ImageThumbnail = "nha_hoa_lan_kieng.jpg", IsActive = true
                },
                new Poi
                {
                    PoiId = 4, CategoryId = 1, Name = "Đền Hùng",
                    Description = "Điểm di tích văn hóa trong khuôn viên Thảo Cầm Viên.",
                    Latitude = 10.7870m, Longitude = 106.7055m, Radius = 25, Priority = 7,
                    ImageThumbnail = "den_hung.jpg", IsActive = true
                },
                new Poi
                {
                    PoiId = 5, CategoryId = 2, Name = "Hà Mã",
                    Description = "Hà mã là loài động vật bán thủy sinh lớn thứ ba trên cạn.",
                    Latitude = 10.7874m, Longitude = 106.7065m, Radius = 25, Priority = 6,
                    ImageThumbnail = "ha_ma.jpg", IsActive = true
                }
            };

            foreach (var poi in seed)
                await db.InsertOrReplaceAsync(poi);
        }

        // ─── GET POIs ─────────────────────────────────────────────────────
        public async Task<List<Poi>> GetAllPoisAsync()
        {
            try
            {
                var db = await GetDbAsync();
                return await db.Table<Poi>().Where(p => p.IsActive).ToListAsync();
            }
            catch (Exception ex) when (IsUnknownTypeError(ex))
            {
                Debug.WriteLine($"[DB] Reopen SQLite mapping for Poi: {ex.Message}");
                var db = await ReopenDbAsync();
                return await db.Table<Poi>().Where(p => p.IsActive).ToListAsync();
            }
        }

        public async Task<Poi?> GetPoiByIdAsync(int poiId)
        {
            try
            {
                var db = await GetDbAsync();
                return await db.Table<Poi>().Where(p => p.PoiId == poiId).FirstOrDefaultAsync();
            }
            catch (Exception ex) when (IsUnknownTypeError(ex))
            {
                Debug.WriteLine($"[DB] Reopen SQLite mapping for PoiById: {ex.Message}");
                var db = await ReopenDbAsync();
                return await db.Table<Poi>().Where(p => p.PoiId == poiId).FirstOrDefaultAsync();
            }
        }

        public async Task SavePoiAsync(Poi poi)
        {
            if (poi == null) return;
            try
            {
                var db = await GetDbAsync();
                await db.InsertOrReplaceAsync(poi);
            }
            catch (Exception ex) when (IsUnknownTypeError(ex))
            {
                Debug.WriteLine($"[DB] Reopen SQLite mapping for SavePoi: {ex.Message}");
                var db = await ReopenDbAsync();
                await db.InsertOrReplaceAsync(poi);
            }
        }

        // ─── LAST VISITED ─────────────────────────────────────────────
        public async Task<Poi?> GetLastVisitedPoiAsync()
        {
            var db = await GetDbAsync();
            PoiVisitHistory? last;
            try
            {
                last = await db.Table<PoiVisitHistory>()
                               .Where(v => v.PoiId != null)
                               .OrderByDescending(v => v.VisitTime)
                               .FirstOrDefaultAsync();
            }
            catch (Exception ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                // Older local DB may miss visit-history table; create it lazily and continue without crash.
                await db.CreateTableAsync<PoiVisitHistory>();
                return null;
            }
            catch (Exception ex) when (IsUnknownTypeError(ex))
            {
                Debug.WriteLine($"[DB] Reopen SQLite mapping for PoiVisitHistory: {ex.Message}");
                db = await ReopenDbAsync();
                last = await db.Table<PoiVisitHistory>()
                               .Where(v => v.PoiId != null)
                               .OrderByDescending(v => v.VisitTime)
                               .FirstOrDefaultAsync();
            }

            if (last?.PoiId == null) return null;
            return await db.Table<Poi>().Where(p => p.PoiId == last.PoiId!.Value).FirstOrDefaultAsync();
        }

        public async Task<Poi?> GetPoiByQrDataAsync(string qrData)
        {
            var db = await GetDbAsync();
            var qr = await db.Table<QrCode>().Where(q => q.QrCodeData == qrData).FirstOrDefaultAsync();
            if (qr == null) return null;
            return await GetPoiByIdAsync(qr.PoiId);
        }

        // ─── GET AUDIO (ưu tiên: ngôn ngữ hiện tại → vi → bất kỳ) ───────
        public async Task<PoiMedium?> GetAudioForPoiAsync(int poiId, string? language = null)
        {
            var db = await GetDbAsync();
            var lang = language ?? CurrentLanguage;

            return await db.Table<PoiMedium>()
                       .Where(m => m.PoiId == poiId && m.MediaType == "audio" && m.Language == lang)
                       .FirstOrDefaultAsync()
                ?? await db.Table<PoiMedium>()
                       .Where(m => m.PoiId == poiId && m.MediaType == "audio" && m.Language == "vi")
                       .FirstOrDefaultAsync()
                ?? await db.Table<PoiMedium>()
                       .Where(m => m.PoiId == poiId && m.MediaType == "audio")
                       .FirstOrDefaultAsync();
        }

        // ─── VISIT LOG ────────────────────────────────────────────────────
        public async Task<long> LogVisitAsync(int poiId, int? userId = null)
        {
            var db = await GetDbAsync();
            var v = new PoiVisitHistory { PoiId = poiId, UserId = userId, VisitTime = DateTime.Now };
            await db.InsertAsync(v);
            return v.VisitId;
        }

        public async Task UpdateListenDurationAsync(long visitId, int seconds)
        {
            var db = await GetDbAsync();
            var v = await db.Table<PoiVisitHistory>().Where(x => x.VisitId == visitId).FirstOrDefaultAsync();
            if (v != null) { v.ListenDuration = seconds; await db.UpdateAsync(v); }
        }
    }
}

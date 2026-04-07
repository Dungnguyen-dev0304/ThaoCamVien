using SQLite;
using SharedThaoCamVien.Models;
using System.Net.Http.Json;

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
        private const string DB_NAME = "tcv_v4.db3";

        // Đổi IP này khi test máy thật:
        // - Máy ảo Android: 10.0.2.2
        // - Máy thật cùng WiFi: IP của máy tính (VD: 192.168.1.100)
        private const string API_BASE = "http://10.0.2.2:5281/api";

        public string CurrentLanguage { get; set; } = "vi";

        // ─── KHỞI TẠO DB ─────────────────────────────────────────────────
        private async Task<SQLiteAsyncConnection> GetDbAsync()
        {
            if (_db != null) return _db;

            var path = Path.Combine(FileSystem.AppDataDirectory, DB_NAME);
            _db = new SQLiteAsyncConnection(path,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            await _db.CreateTableAsync<Poi>();
            await _db.CreateTableAsync<PoiMedium>();
            await _db.CreateTableAsync<QrCode>();
            await _db.CreateTableAsync<PoiVisitHistory>();
            await _db.CreateTableAsync<PoiTranslation>();
            return _db;
        }

        // ─── ĐỒNG BỘ API ─────────────────────────────────────────────────
        public async Task SyncDataFromApiAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                var url = $"{API_BASE}/Pois?lang={CurrentLanguage}";
                var pois = await http.GetFromJsonAsync<List<Poi>>(url);

                if (pois != null && pois.Count > 0)
                {
                    var db = await GetDbAsync();
                    await db.DeleteAllAsync<Poi>();
                    await db.InsertAllAsync(pois);
                    System.Diagnostics.Debug.WriteLine($"[DB] API sync OK: {pois.Count} POIs");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB] API lỗi: {ex.Message} → offline");
            }

            await EnsureOfflineDataAsync();
        }

        // ─── API-FIRST fallback: không seed hardcoded ─────────────────────
        private async Task EnsureOfflineDataAsync()
        {
            var db = await GetDbAsync();
            if (await db.Table<Poi>().CountAsync() > 0)
                return;
            System.Diagnostics.Debug.WriteLine("[DB] API-FIRST mode: không có dữ liệu cache cục bộ.");
        }

        // ─── GET POIs ─────────────────────────────────────────────────────
        public async Task<List<Poi>> GetAllPoisAsync()
        {
            var db = await GetDbAsync();
            return await db.Table<Poi>().Where(p => p.IsActive).ToListAsync();
        }

        public async Task<Poi?> GetPoiByIdAsync(int poiId)
        {
            var db = await GetDbAsync();
            return await db.Table<Poi>().Where(p => p.PoiId == poiId).FirstOrDefaultAsync();
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

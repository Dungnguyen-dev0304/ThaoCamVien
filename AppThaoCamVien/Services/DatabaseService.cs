using SQLite;
using SharedThaoCamVien.Models;
using System.Net.Http.Json;

namespace AppThaoCamVien.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;
        private const string DB_NAME = "thaocamvien_v2.db3"; // Đổi tên để tạo DB mới tinh sạch sẽ

        // BIẾN LƯU NGÔN NGỮ TOÀN CỤC (Mặc định tiếng Việt)
        public string CurrentLanguage { get; set; } = "vi";

        private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            if (_database != null) return _database;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, DB_NAME);
            _database = new SQLiteAsyncConnection(dbPath);

            await _database.CreateTableAsync<Poi>();
            await _database.CreateTableAsync<PoiMedium>();
            await _database.CreateTableAsync<QrCode>();
            await _database.CreateTableAsync<PoiVisitHistory>();

            return _database;
        }

        // ================= ĐỒNG BỘ API & FALLBACK OFFLINE =================
        public async Task SyncDataFromApiAsync()
        {
            try
            {
                // Dùng 10.0.2.2 cho máy ảo Android. Nếu chạy máy thật, đổi thành IP wifi của máy tính (VD: 192.168.1.5)
                string apiUrl = "http://10.0.2.2:5281/api";
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

                var pois = await client.GetFromJsonAsync<List<Poi>>($"{apiUrl}/Pois");
                if (pois != null && pois.Any())
                {
                    var db = await GetDatabaseAsync();
                    await db.DeleteAllAsync<Poi>();
                    await db.InsertAllAsync(pois);
                    System.Diagnostics.Debug.WriteLine("[Sync] Lấy API thành công!");
                    return; // Nếu thành công thì thoát
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sync Lỗi] {ex.Message}. Chuyển sang dùng dữ liệu Offline dự phòng!");
            }

            // FALLBACK DỰ PHÒNG: NẾU API LỖI, BƠM DỮ LIỆU CỨNG ĐỂ DEMO KHÔNG BỊ TRỐNG
            await SeedOfflineDataAsync();
        }

        private async Task SeedOfflineDataAsync()
        {
            var db = await GetDatabaseAsync();
            var count = await db.Table<Poi>().CountAsync();
            if (count > 0) return;

            var pois = new List<Poi>
            {
                new Poi { PoiId=1, Name = "Hổ Đông Dương", Description = "Chào mừng bạn đến với khu vực Hổ Đông Dương. Đây là phân loài hổ đặc hữu đang nguy cấp...", Latitude = 10.7882m, Longitude = 106.7061m, Radius = 30, ImageThumbnail = "tiger.jpg", IsActive = true },
                new Poi { PoiId=2, Name = "Voi Châu Á", Description = "Trước mắt bạn là khu vực Voi Châu Á, loài động vật trên cạn lớn nhất khu vực...", Latitude = 10.7889m, Longitude = 106.7071m, Radius = 40, ImageThumbnail = "elephant.jpg", IsActive = true },
                new Poi { PoiId=3, Name = "Hươu cao cổ", Description = "Khu vực Hươu cao cổ luôn là điểm thu hút du khách nhất với chiếc cổ dài ngoằng...", Latitude = 10.7898m, Longitude = 106.7075m, Radius = 35, ImageThumbnail = "giraffe.jpg", IsActive = true }
            };
            await db.InsertAllAsync(pois);
        }

        // ================= CÁC HÀM GET =================
        public async Task<List<Poi>> GetAllPoisAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Poi>().Where(p => p.IsActive).ToListAsync();
        }

        public async Task<Poi?> GetPoiByIdAsync(int poiId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Poi>().Where(p => p.PoiId == poiId).FirstOrDefaultAsync();
        }

        public async Task<PoiMedium?> GetAudioForPoiAsync(int poiId, string language = null)
        {
            var db = await GetDatabaseAsync();

            // Nếu có truyền ngôn ngữ vào thì dùng, không thì dùng ngôn ngữ mặc định của App
            var langToUse = language ?? CurrentLanguage;

            return await db.Table<PoiMedium>()
                .Where(m => m.PoiId == poiId && m.MediaType == "audio" && m.Language == langToUse)
                .FirstOrDefaultAsync();
        }

        public async Task<Poi?> GetPoiByQrDataAsync(string qrData)
        {
            var db = await GetDatabaseAsync();
            var qrCode = await db.Table<QrCode>().Where(q => q.QrCodeData == qrData).FirstOrDefaultAsync();
            if (qrCode == null) return null;
            return await GetPoiByIdAsync(qrCode.PoiId);
        }

        public async Task<long> LogVisitAsync(int poiId, int? userId = null) { return 1; /* Rút gọn để tránh lỗi */ }
        public async Task UpdateListenDurationAsync(long visitId, int seconds) { }
    }
}
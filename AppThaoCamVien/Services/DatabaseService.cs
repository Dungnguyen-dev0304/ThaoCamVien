using SQLite;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;
        private const string DB_NAME = "thaocamvien.db3";

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

        // ===================== POI =====================

        public async Task<List<Poi>> GetAllPoisAsync()
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Poi>()
                .Where(p => p.IsActive)
                .ToListAsync();
        }

        public async Task<Poi?> GetPoiByIdAsync(int poiId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<Poi>()
                .Where(p => p.PoiId == poiId)
                .FirstOrDefaultAsync();
        }

        // ===================== PoiMedium =====================

        /// <summary>
        /// Lấy URL audio của POI theo ngôn ngữ (mặc định "vi")
        /// </summary>
        public async Task<PoiMedium?> GetAudioForPoiAsync(int poiId, string language = "vi")
        {
            var db = await GetDatabaseAsync();
            return await db.Table<PoiMedium>()
                .Where(m => m.PoiId == poiId
                         && m.MediaType == "audio"
                         && m.Language == language)
                .FirstOrDefaultAsync();
        }

        public async Task<List<PoiMedium>> GetMediaForPoiAsync(int poiId)
        {
            var db = await GetDatabaseAsync();
            return await db.Table<PoiMedium>()
                .Where(m => m.PoiId == poiId)
                .ToListAsync();
        }

        // ===================== QrCode =====================

        /// <summary>
        /// Tìm POI từ QrCodeData được quét bởi camera
        /// </summary>
        public async Task<Poi?> GetPoiByQrDataAsync(string qrData)
        {
            var db = await GetDatabaseAsync();
            var qrCode = await db.Table<QrCode>()
                .Where(q => q.QrCodeData == qrData)
                .FirstOrDefaultAsync();

            if (qrCode == null) return null;
            return await GetPoiByIdAsync(qrCode.PoiId);
        }

        // ===================== VisitHistory =====================

        public async Task<long> LogVisitAsync(int poiId, int? userId = null)
        {
            var db = await GetDatabaseAsync();
            var visit = new PoiVisitHistory
            {
                PoiId = poiId,
                UserId = userId,
                VisitTime = DateTime.Now
            };
            await db.InsertAsync(visit);
            return visit.VisitId;
        }

        public async Task UpdateListenDurationAsync(long visitId, int seconds)
        {
            var db = await GetDatabaseAsync();
            var visit = await db.Table<PoiVisitHistory>()
                .Where(v => v.VisitId == visitId)
                .FirstOrDefaultAsync();
            if (visit != null)
            {
                visit.ListenDuration = seconds;
                await db.UpdateAsync(visit);
            }
        }

        // ===================== Seed Data =====================

        public async Task SeedDataAsync()
        {
            var db = await GetDatabaseAsync();
            var count = await db.Table<Poi>().CountAsync();
            if (count > 0) return;

            var pois = new List<Poi>
            {
                new Poi { Name = "Khu Hổ Đông Dương", Description = "Hổ Đông Dương (Panthera tigris corbetti) là phân loài hổ phân bố ở Đông Nam Á. Khu vực nuôi 3 cá thể được cứu hộ từ nạn buôn bán động vật hoang dã.", Latitude = 10.7872m, Longitude = 106.7058m, Radius = 30, ImageThumbnail = "placeholder_animal.png", IsActive = true, CreatedAt = DateTime.Now },
                new Poi { Name = "Khu Voi Châu Á", Description = "Voi Châu Á (Elephas maximus) nhỏ hơn voi Châu Phi. Thảo Cầm Viên đang bảo tồn 2 cá thể với chương trình giáo dục bảo vệ động vật.", Latitude = 10.7865m, Longitude = 106.7048m, Radius = 35, ImageThumbnail = "placeholder_animal.png", IsActive = true, CreatedAt = DateTime.Now },
                new Poi { Name = "Khu Hươu Cao Cổ", Description = "Hươu cao cổ (Giraffa camelopardalis) là động vật cao nhất thế giới. Đôi hươu tại đây được nhập từ Nam Phi năm 2018.", Latitude = 10.7880m, Longitude = 106.7062m, Radius = 30, ImageThumbnail = "placeholder_animal.png", IsActive = true, CreatedAt = DateTime.Now },
                new Poi { Name = "Khu Chim Nhiệt Đới", Description = "Nhà chim với hơn 50 loài chim nhiệt đới Đông Nam Á. Biểu diễn lúc 10h và 15h hàng ngày.", Latitude = 10.7858m, Longitude = 106.7055m, Radius = 40, ImageThumbnail = "placeholder_animal.png", IsActive = true, CreatedAt = DateTime.Now },
                new Poi { Name = "Khu Linh Trưởng", Description = "Khu bảo tồn các loài khỉ bản địa Việt Nam như Voọc chà vá chân đỏ và Khỉ đuôi lợn — những loài đang cực kỳ nguy cấp.", Latitude = 10.7875m, Longitude = 106.7045m, Radius = 25, ImageThumbnail = "placeholder_animal.png", IsActive = true, CreatedAt = DateTime.Now }
            };

            await db.InsertAllAsync(pois);

            // Seed QR codes (QrCodeData = "TCVN-001", "TCVN-002", ...)
            var insertedPois = await db.Table<Poi>().ToListAsync();
            var qrCodes = insertedPois.Select(p => new QrCode
            {
                PoiId = p.PoiId,
                QrCodeData = $"TCVN-{p.PoiId:D3}",
                CreatedAt = DateTime.Now
            }).ToList();
            await db.InsertAllAsync(qrCodes);
        }
    }
}

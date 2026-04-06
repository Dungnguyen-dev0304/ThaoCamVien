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

        // ─── SEED OFFLINE (15 POI thật từ SQL dump) ──────────────────────
        private async Task EnsureOfflineDataAsync()
        {
            var db = await GetDbAsync();
            if (await db.Table<Poi>().CountAsync() > 0) return;

            var pois = new List<Poi>
            {
                new() { PoiId=1,  CategoryId=3, Name="Bảo tàng",
                    Description="Khu vực bảo tàng lưu giữ tư liệu và hiện vật quý giá về lịch sử Thảo Cầm Viên Sài Gòn từ khi thành lập năm 1864.",
                    Latitude=10.78738006m, Longitude=106.70506044m, Radius=30, Priority=2, ImageThumbnail="bao_tang.jpg", IsActive=true },
                new() { PoiId=2,  CategoryId=3, Name="Đền Hùng",
                    Description="Đền Hùng tưởng nhớ công lao dựng nước của các vua Hùng, là công trình văn hóa tâm linh đặc sắc nằm trong khuôn viên Thảo Cầm Viên.",
                    Latitude=10.78732876m, Longitude=106.70562481m, Radius=30, Priority=2, ImageThumbnail="den_hung.jpg", IsActive=true },
                new() { PoiId=3,  CategoryId=3, Name="Di tích Quán Nhan Hương",
                    Description="Di tích Quán Nhan Hương gắn liền với lịch sử hình thành khu vực, mang giá trị văn hóa và truyền thống đặc sắc của người dân Nam Bộ.",
                    Latitude=10.78599401m, Longitude=106.70751502m, Radius=30, Priority=2, ImageThumbnail="quan_nhan_huong.jpg", IsActive=true },
                new() { PoiId=4,  CategoryId=1, Name="Hà mã",
                    Description="Hà mã (Hippopotamus amphibius) là loài động vật có vú bán thủy sinh lớn nhất. Dù nặng 1.5 đến 3 tấn, hà mã bơi lội cực giỏi và chạy nhanh trên cạn đến 30 km/h.",
                    Latitude=10.78737129m, Longitude=106.70507269m, Radius=25, Priority=1, ImageThumbnail="ha_ma.jpg", IsActive=true },
                new() { PoiId=5,  CategoryId=1, Name="Chuồng hà mã",
                    Description="Khu chuồng hà mã được thiết kế với hồ nước rộng để các cá thể hà mã sinh hoạt đúng với bản năng tự nhiên, giúp chúng có cuộc sống thoải mái nhất.",
                    Latitude=10.78845117m, Longitude=106.70604664m, Radius=25, Priority=1, ImageThumbnail="ha_ma.jpg", IsActive=true },
                new() { PoiId=6,  CategoryId=1, Name="Chuồng khỉ",
                    Description="Khu chuồng khỉ là nhà của nhiều loài linh trưởng thông minh, hoạt bát. Khỉ có khả năng sử dụng công cụ và có cấu trúc xã hội phức tạp, tương tự con người.",
                    Latitude=10.78818245m, Longitude=106.70687755m, Radius=25, Priority=1, ImageThumbnail="chuong_khi.jpg", IsActive=true },
                new() { PoiId=7,  CategoryId=1, Name="Khỉ sóc",
                    Description="Khỉ sóc (Saimiri sciureus) là loài khỉ nhỏ nhất, nặng chỉ 1 kg nhưng có bộ não lớn nhất so với tỷ lệ cơ thể trong các loài linh trưởng. Chúng sống thành đàn lên đến 500 cá thể.",
                    Latitude=10.78712830m, Longitude=106.70755702m, Radius=25, Priority=1, ImageThumbnail="khi_soc.jpg", IsActive=true },
                new() { PoiId=8,  CategoryId=1, Name="Hươu cao cổ",
                    Description="Hươu cao cổ là loài động vật cao nhất trên Trái Đất, có thể cao đến 5.8 mét. Chiếc cổ dài của chúng giúp tiếp cận lá cây ở tầng cao mà các động vật khác không với tới được.",
                    Latitude=10.78989596m, Longitude=106.70753689m, Radius=30, Priority=1, ImageThumbnail="giraffe.jpg", IsActive=true },
                new() { PoiId=9,  CategoryId=1, Name="Chuồng cá sấu",
                    Description="Cá sấu là loài bò sát cổ đại tồn tại hơn 200 triệu năm. Hàm cá sấu có lực cắn mạnh nhất trong thế giới động vật, lên đến 16.000 newton.",
                    Latitude=10.78623964m, Longitude=106.70849144m, Radius=25, Priority=1, ImageThumbnail="chuong_ca_sau.jpg", IsActive=true },
                new() { PoiId=10, CategoryId=1, Name="Khu bò sát",
                    Description="Khu bò sát trưng bày nhiều loài như rắn, thằn lằn, kỳ tôm và rùa. Bò sát là nhóm động vật có xương sống đa dạng nhất, với hơn 10.000 loài được biết đến.",
                    Latitude=10.79018445m, Longitude=106.70581726m, Radius=30, Priority=1, ImageThumbnail="khu_bo_sat.jpg", IsActive=true },
                new() { PoiId=11, CategoryId=1, Name="Khu voi",
                    Description="Voi châu Á (Elephas maximus) là loài động vật trên cạn lớn nhất châu Á. Chúng có trí nhớ đặc biệt, sống theo đàn có cấu trúc xã hội do voi cái dẫn dắt và có thể sống đến 70 tuổi.",
                    Latitude=10.78894106m, Longitude=106.70716114m, Radius=30, Priority=3, ImageThumbnail="elephant.jpg", IsActive=true },
                new() { PoiId=12, CategoryId=1, Name="Khu gấu chó",
                    Description="Gấu chó (Helarctos malayanus) là loài gấu nhỏ nhất thế giới với dấu vệt vàng trên ngực. Chúng là chuyên gia leo trèo với bàn chân hướng vào trong giúp bám chặt cây.",
                    Latitude=10.78611045m, Longitude=106.70791846m, Radius=25, Priority=1, ImageThumbnail="gau.jpg", IsActive=true },
                new() { PoiId=13, CategoryId=2, Name="Vườn bướm",
                    Description="Vườn bướm là không gian thiên nhiên tuyệt đẹp với hàng trăm loài bướm màu sắc rực rỡ. Bướm đóng vai trò thụ phấn quan trọng, giúp duy trì hệ sinh thái cân bằng.",
                    Latitude=10.78762243m, Longitude=106.70641326m, Radius=25, Priority=1, ImageThumbnail="vuon_buom.jpg", IsActive=true },
                new() { PoiId=14, CategoryId=2, Name="Cây di sản Giáng hương",
                    Description="Cây giáng hương quả to (Pterocarpus macrocarpus) là cây di sản quý hiếm với tuổi đời hàng trăm năm. Loài cây này được Hội Bảo vệ Thiên nhiên Việt Nam công nhận là Cây Di sản.",
                    Latitude=10.78792841m, Longitude=106.70791818m, Radius=25, Priority=1, ImageThumbnail="cay_di_san.jpg", IsActive=true },
                new() { PoiId=15, CategoryId=2, Name="Khu hoa lan cây kiểng",
                    Description="Khu sưu tập lan và cây kiểng trưng bày hàng trăm loài hoa lan và cây cảnh quý hiếm từ khắp Việt Nam và thế giới, tạo không gian xanh mát tuyệt vời.",
                    Latitude=10.78611045m, Longitude=106.70791846m, Radius=25, Priority=1, ImageThumbnail="nha_hoa_lan_kieng.jpg", IsActive=true },
            };

            await Task.WhenAll(pois.Select(x => _db.InsertOrReplaceAsync(x)));

            // Seed QR codes TCVN-001 → TCVN-015
            var qrs = pois.Select(p => new QrCode
            {
                PoiId = p.PoiId,
                QrCodeData = $"TCVN-{p.PoiId:D3}",
                CreatedAt = DateTime.Now
            }).ToList();
            await Task.WhenAll(qrs.Select(x => _db.InsertOrReplaceAsync(x)));

            System.Diagnostics.Debug.WriteLine("[DB] Offline seed: 15 POIs + 15 QR codes");
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

using SQLite;
using SharedThaoCamVien.Models;
using System.Net.Http.Json;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// DatabaseService quản lý toàn bộ dữ liệu local (SQLite) và đồng bộ từ API.
    /// 
    /// Chiến lược "Offline First":
    /// 1. Khi có mạng: gọi API với ?lang={ngôn ngữ hiện tại}, lưu xuống SQLite
    /// 2. Khi mất mạng: dùng dữ liệu SQLite đã lưu trước đó
    /// 3. Nếu SQLite cũng rỗng: dùng seed data hardcode để app không bị trắng màn hình
    /// </summary>
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;
        private const string DB_NAME = "thaocamvien_v3.db3";

        // API URL — đổi thành IP thật khi test máy thật
        // Máy ảo Android: 10.0.2.2 | Máy thật: IP WiFi của máy tính (VD: 192.168.1.5)
        private const string API_BASE_URL = "http://10.0.2.2:5281/api";

        // Ngôn ngữ hiện tại của app — dùng để gọi API đúng ngôn ngữ
        public string CurrentLanguage { get; set; } = "vi";

        // ===================== DB INIT =====================
        private async Task<SQLiteAsyncConnection> GetDatabaseAsync()
        {
            if (_database != null) return _database;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, DB_NAME);
            _database = new SQLiteAsyncConnection(dbPath);

            // Tạo tất cả bảng cần thiết
            await _database.CreateTableAsync<Poi>();
            await _database.CreateTableAsync<PoiMedium>();
            await _database.CreateTableAsync<QrCode>();
            await _database.CreateTableAsync<PoiVisitHistory>();
            await _database.CreateTableAsync<PoiTranslation>(); // Bảng bản dịch

            return _database;
        }

        // ===================== SYNC TỪ API =====================
        /// <summary>
        /// Đồng bộ dữ liệu POI từ API server với đúng ngôn ngữ hiện tại.
        /// Tự động fallback về offline nếu có lỗi.
        /// </summary>
        public async Task SyncDataFromApiAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

                // Truyền lang vào query param để server trả về đúng ngôn ngữ
                var url = $"{API_BASE_URL}/Pois?lang={CurrentLanguage}";
                System.Diagnostics.Debug.WriteLine($"[Sync] GET {url}");

                var pois = await client.GetFromJsonAsync<List<Poi>>(url);

                if (pois != null && pois.Any())
                {
                    var db = await GetDatabaseAsync();

                    // Xóa và insert lại để đảm bảo dữ liệu luôn mới nhất
                    await db.DeleteAllAsync<Poi>();
                    await db.InsertAllAsync(pois);

                    System.Diagnostics.Debug.WriteLine(
                        $"[Sync] ✅ Lấy được {pois.Count} POI (lang: {CurrentLanguage})");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Sync] ⚠️ API lỗi ({ex.Message}) → dùng dữ liệu offline");
            }

            // Fallback: dùng seed data cứng
            await SeedOfflineDataAsync();
        }

        // ===================== SEED OFFLINE =====================
        private async Task SeedOfflineDataAsync()
        {
            var db = await GetDatabaseAsync();
            var count = await db.Table<Poi>().CountAsync();
            if (count > 0) return; // Đã có dữ liệu cũ → giữ nguyên

            System.Diagnostics.Debug.WriteLine("[Sync] 📦 Seed offline data...");

            // Dữ liệu thật từ SQL dump của bạn (15 POI)
            var pois = new List<Poi>
            {
                new() { PoiId=1,  CategoryId=3, Name="Bảo tàng",
                    Description="Đây là khu vực bảo tàng, nơi lưu giữ nhiều tư liệu và hiện vật quý giá, giúp du khách hiểu thêm về lịch sử và sự phát triển của khu vực này.",
                    Latitude=10.78738006m, Longitude=106.70506044m, Radius=30, Priority=1, ImageThumbnail="bao_tang.jpg", IsActive=true },
                new() { PoiId=2,  CategoryId=3, Name="Đền Hùng",
                    Description="Đây là Đền Hùng, một công trình mang ý nghĩa tâm linh, tưởng nhớ công lao dựng nước của các vua Hùng trong lịch sử dân tộc.",
                    Latitude=10.78732876m, Longitude=106.70562481m, Radius=30, Priority=1, ImageThumbnail="den_hung.jpg", IsActive=true },
                new() { PoiId=3,  CategoryId=3, Name="Di tích lịch sử Quán Nhan Hương",
                    Description="Đây là di tích Quán Nhan Hương, gắn liền với lịch sử hình thành và phát triển của khu vực, mang giá trị văn hóa và truyền thống đặc sắc.",
                    Latitude=10.78599401m, Longitude=106.70751502m, Radius=30, Priority=1, ImageThumbnail="quan_nhan_huong.jpg", IsActive=true },
                new() { PoiId=4,  CategoryId=1, Name="Hà mã",
                    Description="Đây là khu vực sinh sống của hà mã, loài động vật lớn sống chủ yếu dưới nước, nổi bật với thân hình đồ sộ và tính cách khá hiền lành nhưng cũng rất mạnh mẽ.",
                    Latitude=10.78737129m, Longitude=106.70507269m, Radius=25, Priority=1, ImageThumbnail="ha_ma.jpg", IsActive=true },
                new() { PoiId=5,  CategoryId=1, Name="Chuồng hà mã",
                    Description="Đây là chuồng hà mã, nơi chăm sóc và bảo tồn loài động vật bán thủy sinh đặc biệt này trong môi trường gần gũi với tự nhiên.",
                    Latitude=10.78845117m, Longitude=106.70604664m, Radius=25, Priority=1, ImageThumbnail="ha_ma.jpg", IsActive=true },
                new() { PoiId=6,  CategoryId=1, Name="Chuồng khỉ",
                    Description="Đây là khu chuồng khỉ, nơi sinh sống của nhiều loài khỉ với tập tính nhanh nhẹn, thông minh và rất thân thiện với môi trường xung quanh.",
                    Latitude=10.78818245m, Longitude=106.70687755m, Radius=25, Priority=1, ImageThumbnail="chuong_khi.jpg", IsActive=true },
                new() { PoiId=7,  CategoryId=1, Name="Khỉ sóc",
                    Description="Đây là khu vực của khỉ sóc, loài khỉ nhỏ nhắn, linh hoạt, nổi bật với khả năng leo trèo nhanh và tính cách hoạt bát.",
                    Latitude=10.78712830m, Longitude=106.70755702m, Radius=25, Priority=1, ImageThumbnail="khi_soc.jpg", IsActive=true },
                new() { PoiId=8,  CategoryId=1, Name="Hươu cao cổ",
                    Description="Đây là khu vực hươu cao cổ, loài động vật nổi bật với chiếc cổ dài đặc trưng, hiền lành và thường sinh sống trong môi trường thảo nguyên.",
                    Latitude=10.78989596m, Longitude=106.70753689m, Radius=30, Priority=1, ImageThumbnail="giraffe.jpg", IsActive=true },
                new() { PoiId=9,  CategoryId=1, Name="Chuồng cá sấu",
                    Description="Đây là khu chuồng cá sấu, nơi nuôi dưỡng loài bò sát săn mồi nguy hiểm, nổi bật với hàm răng sắc bén và khả năng ẩn mình dưới nước.",
                    Latitude=10.78623964m, Longitude=106.70849144m, Radius=25, Priority=1, ImageThumbnail="chuong_ca_sau.jpg", IsActive=true },
                new() { PoiId=10, CategoryId=1, Name="Khu bò sát",
                    Description="Đây là khu bò sát, nơi trưng bày và bảo tồn nhiều loài như rắn, thằn lằn và các loài đặc trưng khác của hệ sinh thái.",
                    Latitude=10.79018445m, Longitude=106.70581726m, Radius=30, Priority=1, ImageThumbnail="khu_bo_sat.jpg", IsActive=true },
                new() { PoiId=11, CategoryId=1, Name="Khu voi",
                    Description="Đây là khu voi, nơi sinh sống của những chú voi to lớn, thân thiện và thông minh, một trong những loài động vật nổi bật nhất.",
                    Latitude=10.78894106m, Longitude=106.70716114m, Radius=30, Priority=1, ImageThumbnail="elephant.jpg", IsActive=true },
                new() { PoiId=12, CategoryId=1, Name="Khu gấu chó",
                    Description="Đây là khu gấu chó, loài gấu đặc trưng với dấu vệt hình chữ V trên ngực, có tập tính leo trèo và kiếm ăn linh hoạt.",
                    Latitude=10.78611045m, Longitude=106.70791846m, Radius=25, Priority=1, ImageThumbnail="gau.jpg", IsActive=true },
                new() { PoiId=13, CategoryId=2, Name="Vườn bướm",
                    Description="Đây là vườn bướm, nơi sinh sống của nhiều loài bướm với màu sắc rực rỡ, tạo nên không gian thiên nhiên sinh động và hấp dẫn.",
                    Latitude=10.78762243m, Longitude=106.70641326m, Radius=25, Priority=1, ImageThumbnail="vuon_buom.jpg", IsActive=true },
                new() { PoiId=14, CategoryId=2, Name="Cây di sản – Giáng hương quả to",
                    Description="Đây là cây di sản giáng hương quả to, một loài cây quý hiếm với tuổi đời lâu năm, mang giá trị sinh thái và bảo tồn cao.",
                    Latitude=10.78792841m, Longitude=106.70791818m, Radius=25, Priority=1, ImageThumbnail="cay_di_san.jpg", IsActive=true },
                new() { PoiId=15, CategoryId=2, Name="Khu sưu tập hoa lan cây kiểng",
                    Description="Đây là khu sưu tập hoa lan và cây kiểng, nơi trưng bày nhiều loài hoa đẹp và cây cảnh quý hiếm, tạo nên không gian xanh mát.",
                    Latitude=10.78611045m, Longitude=106.70791846m, Radius=25, Priority=1, ImageThumbnail="nha_hoa_lan_kieng.jpg", IsActive=true },
            };

            foreach (var item in pois)
            {
                await db.InsertOrReplaceAsync(item);
            }

            // Seed QR codes
            var qrCodes = pois.Select(p => new QrCode
            {
                PoiId = p.PoiId,
                QrCodeData = $"TCVN-{p.PoiId:D3}",
                CreatedAt = DateTime.Now
            }).ToList();
            foreach (var item in qrCodes)
            {
                await db.InsertOrReplaceAsync(item);
            }

            System.Diagnostics.Debug.WriteLine("[Sync] ✅ Seed offline hoàn tất (15 POIs + 15 QR codes)");
        }

        // ===================== GET POI =====================
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

        public async Task<Poi?> GetPoiByQrDataAsync(string qrData)
        {
            var db = await GetDatabaseAsync();
            var qr = await db.Table<QrCode>().Where(q => q.QrCodeData == qrData).FirstOrDefaultAsync();
            if (qr == null) return null;
            return await GetPoiByIdAsync(qr.PoiId);
        }

        // ===================== GET AUDIO =====================
        /// <summary>
        /// Lấy URL audio cho POI theo ngôn ngữ.
        /// Thứ tự ưu tiên: ngôn ngữ hiện tại → tiếng Việt → bất kỳ audio nào có sẵn.
        /// </summary>
        public async Task<PoiMedium?> GetAudioForPoiAsync(int poiId, string? language = null)
        {
            var db = await GetDatabaseAsync();
            var lang = language ?? CurrentLanguage;

            // Ưu tiên 1: đúng ngôn ngữ yêu cầu
            var media = await db.Table<PoiMedium>()
                .Where(m => m.PoiId == poiId && m.MediaType == "audio" && m.Language == lang)
                .FirstOrDefaultAsync();

            if (media != null) return media;

            // Ưu tiên 2: fallback tiếng Việt
            if (lang != "vi")
            {
                media = await db.Table<PoiMedium>()
                    .Where(m => m.PoiId == poiId && m.MediaType == "audio" && m.Language == "vi")
                    .FirstOrDefaultAsync();
            }

            // Ưu tiên 3: bất kỳ audio nào có sẵn cho POI này
            return media ?? await db.Table<PoiMedium>()
                .Where(m => m.PoiId == poiId && m.MediaType == "audio")
                .FirstOrDefaultAsync();
        }

        // ===================== VISIT LOG =====================
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
                .Where(v => v.VisitId == visitId).FirstOrDefaultAsync();
            if (visit != null)
            {
                visit.ListenDuration = seconds;
                await db.UpdateAsync(visit);
            }
        }
    }
}

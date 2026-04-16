using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;
using ApiThaoCamVien.Models;

namespace ApiThaoCamVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PoisController : ControllerBase
    {
        private readonly WebContext _ctx;
        private readonly ILogger<PoisController> _logger;

        public PoisController(WebContext ctx, ILogger<PoisController> logger)
        {
            _ctx = ctx;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/Pois
        /// GET /api/Pois?lang=en
        /// Test URL (browser): http://localhost:5281/api/Pois
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string lang = "vi")
        {
            try
            {
                _logger.LogInformation("GetAll called, lang={Lang}", lang);

                var pois = await _ctx.Pois
                    .OrderByDescending(p => p.Priority)
                    .ToListAsync();

                // Nếu DB chưa set `IsActive=true` cho record nào,
                // app sẽ luôn nhận list rỗng. Fallback để bạn nhìn thấy dữ liệu ngay.
                if (pois.Count > 0 && pois.All(p => p.IsActive == false))
                {
                    _logger.LogWarning("No active POIs found (IsActive=false for all). Returning all POIs for debug.");
                }

                pois = pois
                    .Where(p => p.IsActive == true)
                    .ToList();

                if (pois.Count == 0)
                {
                    // Fallback: nếu không có active thì trả về toàn bộ để tránh UI trắng.
                    _logger.LogWarning("No active POIs found. Falling back to returning all POIs.");
                    pois = await _ctx.Pois
                        .OrderByDescending(p => p.Priority)
                        .ToListAsync();
                }

                _logger.LogInformation("Found {Count} active POIs", pois.Count);

                if (pois.Count == 0)
                {
                    _logger.LogWarning("No POIs found in database! Run seed script.");
                    return Ok(new List<Poi>()); // Trả về array rỗng, không phải 404
                }

                // Áp dụng bản dịch nếu cần
                if (lang != "vi" && _ctx.Database.CanConnect())
                {
                    try
                    {
                        var hasTranslations = await _ctx.Database
                            .ExecuteSqlRawAsync("SELECT 1") >= 0;

                        var transTable = _ctx.Model.FindEntityType(typeof(PoiTranslation));
                        if (transTable != null)
                        {
                            var trans = await _ctx.PoiTranslations
                                .Where(t => t.LanguageCode == lang)
                                .ToDictionaryAsync(t => t.PoiId);

                            foreach (var poi in pois)
                            {
                                if (trans.TryGetValue(poi.PoiId, out var t))
                                {
                                    if (!string.IsNullOrWhiteSpace(t.Name))
                                        poi.Name = t.Name;
                                    if (!string.IsNullOrWhiteSpace(t.Description))
                                        poi.Description = t.Description;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Bảng poi_translations chưa tồn tại → bỏ qua, trả về tiếng Việt
                        _logger.LogWarning("Translations not available: {Msg}", ex.Message);
                    }
                }

                return Ok(pois);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAll");
                return StatusCode(500, new { message = ex.Message, detail = ex.ToString() });
            }
        }

        /// <summary>GET /api/Pois/5?lang=en</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOne(int id, [FromQuery] string lang = "vi")
        {
            var poi = await _ctx.Pois.FindAsync(id);
            if (poi == null)
                return NotFound(new { message = $"POI #{id} không tồn tại" });

            if (lang != "vi")
            {
                try
                {
                    var t = await _ctx.PoiTranslations
                        .FirstOrDefaultAsync(x => x.PoiId == id && x.LanguageCode == lang);
                    if (t != null)
                    {
                        if (!string.IsNullOrWhiteSpace(t.Name)) poi.Name = t.Name;
                        if (!string.IsNullOrWhiteSpace(t.Description)) poi.Description = t.Description;
                    }
                }
                catch { /* translations table may not exist */ }
            }

            return Ok(poi);
        }

        /// <summary>GET /api/Pois/qr/TCVN-008?lang=en</summary>
        [HttpGet("qr/{qrData}")]
        public async Task<IActionResult> GetByQr(string qrData, [FromQuery] string lang = "vi")
        {
            var qr = await _ctx.QrCodes
                .FirstOrDefaultAsync(q => q.QrCodeData == qrData);
            if (qr == null)
                return NotFound(new { message = $"QR '{qrData}' chưa đăng ký" });
            return await GetOne(qr.PoiId, lang);
        }

        /// <summary>GET /api/Pois/numpad/8?lang=en</summary>
        [HttpGet("numpad/{poiId:int}")]
        public Task<IActionResult> GetByNumpad(int poiId, [FromQuery] string lang = "vi")
            => GetOne(poiId, lang);

        /// <summary>
        /// GET /api/Pois/health — kiểm tra API và DB có hoạt động không
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            try
            {
                var count = await _ctx.Pois.CountAsync();
                return Ok(new
                {
                    status = "ok",
                    poi_count = count,
                    database = "connected",
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }

        // ============================================================
        // PHẦN DƯƠNG CODE
        // ENDPOINT 1: GET /api/Pois/category/{categoryId}
        // Lọc POI theo danh mục (1=Động vật, 2=Thực vật, 3=Di tích)
        // ============================================================
        [HttpGet("category/{categoryId:int}")]
        public async Task<IActionResult> GetByCategory(int categoryId, [FromQuery] string lang = "vi")
        {
            try
            {
                var pois = await _ctx.Pois
                    .Where(p => p.CategoryId == categoryId && p.IsActive == true)
                    .OrderByDescending(p => p.Priority)
                    .ToListAsync();

                if (lang != "vi")
                {
                    try
                    {
                        var trans = await _ctx.PoiTranslations
                            .Where(t => t.LanguageCode == lang && pois.Select(p => p.PoiId).Contains(t.PoiId))
                            .ToDictionaryAsync(t => t.PoiId);

                        foreach (var poi in pois)
                        {
                            if (trans.TryGetValue(poi.PoiId, out var t))
                            {
                                if (!string.IsNullOrWhiteSpace(t.Name)) poi.Name = t.Name;
                                if (!string.IsNullOrWhiteSpace(t.Description)) poi.Description = t.Description;
                            }
                        }
                    }
                    catch { /* bảng translations chưa có thì bỏ qua */ }
                }

                return Ok(pois);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetByCategory");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ============================================================
        // ENDPOINT 2: POST /api/Pois/{id}/visit
        // App gọi khi user mở xem 1 POI, ghi vào bảng poi_visit_history
        // Body JSON: { "userId": 5, "listenDuration": 30 }
        // userId có thể null nếu chưa đăng nhập
        // ============================================================
        [HttpPost("{id:int}/visit")]
        public async Task<IActionResult> RecordVisit(int id, [FromBody] VisitRequest request)
        {
            try
            {
                var poi = await _ctx.Pois.FindAsync(id);
                if (poi == null)
                    return NotFound(new { message = $"POI #{id} không tồn tại" });

                var visit = new PoiVisitHistory
                {
                    PoiId = id,
                    UserId = request.UserId,       // null nếu chưa đăng nhập
                    VisitTime = DateTime.Now,
                    ListenDuration = request.ListenDuration // null nếu chưa nghe xong
                };

                _ctx.PoiVisitHistories.Add(visit);
                await _ctx.SaveChangesAsync();

                _logger.LogInformation("Visit recorded: POI #{PoiId}, User #{UserId}, Duration {Duration}s",
                    id, request.UserId, request.ListenDuration);

                return Ok(new { message = "Đã ghi nhận lượt thăm", visitId = visit.VisitId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RecordVisit");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ============================================================
        // ENDPOINT 3: GET /api/Pois/nearby?lat=10.787&lng=106.706&lang=vi
        // Tìm POI trong bán kính (dùng cột Radius của từng POI, mặc định 15m)
        // App gọi liên tục khi user di chuyển trong vườn
        // ============================================================
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] string lang = "vi")
        {
            try
            {
                // Lấy tất cả POI active từ DB (tọa độ dạng decimal)
                var allPois = await _ctx.Pois
                    .Where(p => p.IsActive == true)
                    .ToListAsync();

                // Tính khoảng cách Haversine phía C# (SQL Server không có hàm này sẵn)
                var nearby = allPois
                    .Select(p => new
                    {
                        Poi = p,
                        Distance = HaversineMeters(lat, lng, (double)p.Latitude, (double)p.Longitude)
                    })
                    .Where(x => x.Distance <= (x.Poi.Radius ?? 15)) // dùng Radius của từng POI
                    .OrderBy(x => x.Distance)
                    .Select(x => new
                    {
                        x.Poi.PoiId,
                        x.Poi.Name,
                        x.Poi.CategoryId,
                        x.Poi.Latitude,
                        x.Poi.Longitude,
                        x.Poi.Description,
                        x.Poi.ImageThumbnail,
                        x.Poi.Radius,
                        DistanceMeters = Math.Round(x.Distance, 1)
                    })
                    .ToList();

                if (lang != "vi" && nearby.Any())
                {
                    // Trả về dữ liệu gốc nếu không có translation (không crash)
                }

                _logger.LogInformation("Nearby check at ({Lat},{Lng}): {Count} POIs found", lat, lng, nearby.Count);

                return Ok(nearby);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetNearby");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/Pois/{id}/translations
        // Lấy tất cả bản dịch có sẵn của 1 POI
        // App dùng để hiển thị danh sách ngôn ngữ người dùng có thể chọn
        // Ví dụ: POI #3 có vi, en, fr → app hiển thị 3 nút ngôn ngữ
        // ─────────────────────────────────────────────────────────
        [HttpGet("{id:int}/translations")]
        public async Task<IActionResult> GetTranslations(int id)
        {
            try
            {
                var poi = await _ctx.Pois.FindAsync(id);
                if (poi == null)
                    return NotFound(new { message = $"POI #{id} không tồn tại" });

                var translations = await _ctx.PoiTranslations
                    .Where(t => t.PoiId == id)
                    .Select(t => new
                    {
                        t.TranslationId,
                        t.PoiId,
                        t.LanguageCode,
                        t.Name,
                        t.Description
                    })
                    .ToListAsync();

                // Luôn thêm tiếng Việt gốc vào đầu danh sách (vì vi không lưu trong poi_translations)
                var result = new List<object>
        {
            new
            {
                TranslationId = 0,
                PoiId         = id,
                LanguageCode  = "vi",
                poi.Name,
                poi.Description
            }
        };
                result.AddRange(translations.Cast<object>());

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTranslations");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ============================================================
        // HELPER: Công thức Haversine tính khoảng cách 2 tọa độ (đơn vị: mét)
        // ============================================================
        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // bán kính Trái Đất (mét)
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }


}
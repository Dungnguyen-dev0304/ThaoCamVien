using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;
using ApiThaoCamVien.Models;
using ApiThaoCamVien.Services;

namespace ApiThaoCamVien.Controllers
{
    [Route("api/Pois")]
    [ApiController]
    public class PoiExtController : ControllerBase
    {
        private readonly WebContext _ctx;
        private readonly ILogger<PoiExtController> _logger;
        private readonly PoiLocalizationService _poiLocalization;

        public PoiExtController(WebContext ctx, ILogger<PoiExtController> logger, PoiLocalizationService poiLocalization)
        {
            _ctx = ctx;
            _logger = logger;
            _poiLocalization = poiLocalization;
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/Pois/category/{categoryId}
        // Lọc POI theo danh mục (1=Động vật, 2=Thực vật, 3=Di tích)
        // ─────────────────────────────────────────────────────────
        [HttpGet("category/{categoryId:int}")]
        public async Task<IActionResult> GetByCategory(int categoryId, [FromQuery] string lang = "vi")
        {
            try
            {
                var pois = await _ctx.Pois
                    .Where(p => p.CategoryId == categoryId && p.IsActive == true)
                    .OrderByDescending(p => p.Priority)
                    .ToListAsync();

                lang = (lang ?? "vi").ToLowerInvariant();
                if (lang != "vi")
                    await _poiLocalization.ApplyToPoisAsync(pois, lang, HttpContext.RequestAborted);

                return Ok(pois);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetByCategory");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // POST /api/Pois/{id}/visit
        // App gọi khi user mở xem 1 POI, ghi vào bảng poi_visit_history
        // Body JSON: { "userId": 5, "listenDuration": 30 }
        // ─────────────────────────────────────────────────────────
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
                    VisitTime = DateTime.Now,
                    ListenDuration = request.ListenDuration
                };

                _ctx.PoiVisitHistories.Add(visit);
                await _ctx.SaveChangesAsync();

                _logger.LogInformation(
                    "Visit recorded: POI #{PoiId}, Duration {Duration}s",
                    id, request.ListenDuration);

                return Ok(new { message = "Đã ghi nhận lượt thăm", visitId = visit.VisitId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RecordVisit");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // PATCH /api/Pois/visit/{visitId}/duration
        // App gọi khi user dừng / đóng audio để cập nhật tổng số giây đã nghe.
        // Body JSON: { "seconds": 42 }
        // Phương án C: Start tạo record → Stop PATCH cập nhật duration.
        // ─────────────────────────────────────────────────────────
        [HttpPatch("visit/{visitId:long}/duration")]
        public async Task<IActionResult> UpdateVisitDuration(long visitId, [FromBody] UpdateDurationRequest request)
        {
            try
            {
                if (request == null || request.Seconds < 0)
                    return BadRequest(new { message = "seconds phải >= 0" });

                var visit = await _ctx.PoiVisitHistories.FirstOrDefaultAsync(v => v.VisitId == visitId);
                if (visit == null)
                    return NotFound(new { message = $"Visit #{visitId} không tồn tại" });

                visit.ListenDuration = request.Seconds;
                await _ctx.SaveChangesAsync();

                _logger.LogInformation(
                    "Visit duration updated: #{VisitId} = {Seconds}s",
                    visitId, request.Seconds);

                return Ok(new { message = "Đã cập nhật thời gian nghe", visitId, seconds = request.Seconds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateVisitDuration");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/Pois/media?type=audio
        // App mobile gọi để sync metadata audio xuống SQLite local.
        //
        // Nguồn thật sự: bảng poi_audios (admin upload qua AudioController).
        // Ở đây project sang shape của PoiMedium để app không phải sửa:
        //   AudioId       → MediaId
        //   PoiId         → PoiId
        //   "audio"       → MediaType
        //   FilePath      → MediaUrl (vd "/audio/pois/tiger.mp3")
        //   LanguageCode  → Language
        // MediaUrl là đường dẫn tương đối; app ghép host qua
        // MediaUrlResolver (port 5181 của Web) khi phát.
        // ─────────────────────────────────────────────────────────
        [HttpGet("media")]
        public async Task<IActionResult> GetAllMedia([FromQuery] string? type = null)
        {
            try
            {
                // Hiện admin chỉ upload audio qua /Audio/Manage, nên không
                // phân biệt image ở đây. Param `type` để tương lai mở rộng.
                if (!string.IsNullOrWhiteSpace(type)
                    && !type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                {
                    return Ok(Array.Empty<object>());
                }

                // Lấy bản ghi thuần — chưa format chuỗi để EF Core dịch ra SQL được.
                var raw = await _ctx.PoiAudios.AsNoTracking()
                    .Where(a => a.FilePath != null && a.FilePath != "")
                    .Select(a => new
                    {
                        a.AudioId,
                        a.PoiId,
                        a.FilePath,
                        a.LanguageCode,
                        a.UpdatedAt
                    })
                    .ToListAsync();

                // Nhúng UpdatedAt vào MediaUrl dưới dạng query string `?v=...`
                // để app mobile coi file mới upload là URL khác ⇒ cache miss ⇒ tải lại.
                // Static-file middleware của ASP.NET Core tự bỏ qua query string khi
                // phục vụ file nên URL này vẫn trỏ đúng đến file vật lý.
                var list = raw.Select(a => new
                {
                    MediaId = a.AudioId,
                    PoiId = a.PoiId,
                    MediaType = "audio",
                    MediaUrl = $"{a.FilePath}?v={a.UpdatedAt:yyyyMMddHHmmss}",
                    Language = a.LanguageCode
                }).ToList();

                _logger.LogInformation(
                    "GetAllMedia returned {Count} audio rows (from poi_audios, versioned by UpdatedAt)",
                    list.Count);

                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAllMedia");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/Pois/nearby?lat=10.787&lng=106.706
        // Tìm POI trong bán kính GPS hiện tại của người dùng
        // App gọi liên tục khi user di chuyển trong vườn
        // ─────────────────────────────────────────────────────────
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby(
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] string lang = "vi")
        {
            try
            {
                var allPois = await _ctx.Pois
                    .Where(p => p.IsActive == true)
                    .ToListAsync();

                var nearby = allPois
                    .Select(p => new
                    {
                        Poi = p,
                        Distance = HaversineMeters(lat, lng, (double)p.Latitude, (double)p.Longitude)
                    })
                    .Where(x => x.Distance <= (x.Poi.Radius ?? 15))
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

                _logger.LogInformation(
                    "Nearby check at ({Lat},{Lng}): {Count} POIs found",
                    lat, lng, nearby.Count);

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

                // Luôn thêm tiếng Việt gốc vào đầu (vi không lưu trong poi_translations)
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

        // ─────────────────────────────────────────────────────────
        // HELPER: Công thức Haversine tính khoảng cách 2 tọa độ (mét)
        // ─────────────────────────────────────────────────────────
        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000;
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }
}
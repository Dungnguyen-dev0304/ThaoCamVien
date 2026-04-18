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
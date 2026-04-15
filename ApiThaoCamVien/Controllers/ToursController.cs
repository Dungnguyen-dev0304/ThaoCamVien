using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiThaoCamVien.Models;

namespace ApiThaoCamVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ToursController : ControllerBase
    {
        private readonly WebContext _ctx;
        private readonly ILogger<ToursController> _logger;

        private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

        public ToursController(WebContext ctx, ILogger<ToursController> logger)
        {
            _ctx = ctx;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/Tours
        // Lấy danh sách tất cả tour đang active
        // App dùng để hiển thị màn hình chọn tour
        // ─────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var tours = await _ctx.Tours
                    .Where(t => t.IsActive == true)
                    .OrderBy(t => t.TourId)
                    .Select(t => new
                    {
                        t.TourId,
                        t.Name,
                        t.Description,
                        t.EstimatedTime,   // phút ước tính
                        t.IsActive,
                        t.CreatedAt,
                        PoiCount = t.TourPois.Count // số điểm trong tour
                    })
                    .ToListAsync();

                return Ok(tours);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAll Tours");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // ─────────────────────────────────────────────────────────
        // GET /api/Tours/{id}
        // Lấy chi tiết 1 tour + danh sách POI theo thứ tự
        // App dùng để hiển thị bản đồ tour với các điểm dừng
        // ─────────────────────────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOne(int id, [FromQuery] string lang = "vi")
        {
            try
            {
                var tour = await _ctx.Tours
                    .Where(t => t.TourId == id)
                    .FirstOrDefaultAsync();

                if (tour == null)
                    return NotFound(new { message = $"Tour #{id} không tồn tại" });

                // Lấy danh sách POI trong tour theo thứ tự OrderIndex
                var tourPois = await _ctx.TourPois
                    .Where(tp => tp.TourId == id)
                    .OrderBy(tp => tp.OrderIndex)
                    .Include(tp => tp.Poi)
                    .ToListAsync();

                // Áp dụng translation nếu cần
                if (lang != "vi")
                {
                    try
                    {
                        var poiIds = tourPois
                            .Where(tp => tp.Poi != null)
                            .Select(tp => tp.PoiId!.Value)
                            .ToList();

                        var trans = await _ctx.PoiTranslations
                            .Where(t => t.LanguageCode == lang && poiIds.Contains(t.PoiId))
                            .ToDictionaryAsync(t => t.PoiId);

                        foreach (var tp in tourPois)
                        {
                            if (tp.Poi == null) continue;
                            if (trans.TryGetValue(tp.Poi.PoiId, out var t))
                            {
                                if (!string.IsNullOrWhiteSpace(t.Name)) tp.Poi.Name = t.Name;
                                if (!string.IsNullOrWhiteSpace(t.Description)) tp.Poi.Description = t.Description;
                            }
                        }
                    }
                    catch { /* translations chưa có thì bỏ qua */ }
                }

                var result = new
                {
                    tour.TourId,
                    tour.Name,
                    tour.Description,
                    tour.EstimatedTime,
                    tour.IsActive,
                    tour.CreatedAt,
                    Pois = tourPois.Select(tp => new
                    {
                        tp.Id,
                        tp.OrderIndex,
                        tp.PoiId,
                        tp.Poi!.Name,
                        tp.Poi.CategoryId,
                        tp.Poi.Latitude,
                        tp.Poi.Longitude,
                        tp.Poi.Description,
                        tp.Poi.ImageThumbnail,
                        tp.Poi.Radius,
                        // Trả về URL ảnh đầy đủ luôn cho app dùng
                        ImageUrl = string.IsNullOrEmpty(tp.Poi.ImageThumbnail)
                            ? null
                            : $"{BaseUrl}/images/pois/{tp.Poi.ImageThumbnail}"
                    })
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOne Tour");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
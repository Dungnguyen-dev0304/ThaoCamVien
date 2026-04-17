using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiThaoCamVien.Models;
using ApiThaoCamVien;
using ApiThaoCamVien.Services;

namespace ApiThaoCamVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ToursController : ControllerBase
    {
        private readonly WebContext _ctx;
        private readonly ILogger<ToursController> _logger;
        private readonly PoiLocalizationService _poiLocalization;

        public ToursController(WebContext ctx, ILogger<ToursController> logger, PoiLocalizationService poiLocalization)
        {
            _ctx = ctx;
            _logger = logger;
            _poiLocalization = poiLocalization;
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

                lang = (lang ?? "vi").ToLowerInvariant();

                // Lấy danh sách POI trong tour theo thứ tự OrderIndex
                var tourPois = await _ctx.TourPois
                    .Where(tp => tp.TourId == id)
                    .OrderBy(tp => tp.OrderIndex)
                    .Include(tp => tp.Poi)
                    .ToListAsync();

                var tourPoiEntities = tourPois.Where(tp => tp.Poi != null).Select(tp => tp.Poi!).ToList();
                if (lang != "vi" && tourPoiEntities.Count > 0)
                    await _poiLocalization.ApplyToPoisAsync(tourPoiEntities, lang, HttpContext.RequestAborted);

                if (lang != "vi")
                {
                    tour.Name = await _poiLocalization.TranslatePlainAsync(tour.Name ?? "", lang, HttpContext.RequestAborted);
                    tour.Description = await _poiLocalization.TranslatePlainAsync(tour.Description ?? "", lang, HttpContext.RequestAborted);
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
                        ImageUrl = string.IsNullOrEmpty(tp.Poi.ImageThumbnail)
                            ? null
                            : PoiMediaUrls.ResolveThumbnail(Request, tp.Poi.ImageThumbnail)
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
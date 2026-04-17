using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;
using ApiThaoCamVien.Models;
using ApiThaoCamVien;

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

                foreach (var poi in pois)
                    poi.ImageThumbnail = PoiMediaUrls.ResolveThumbnail(Request, poi.ImageThumbnail);

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

            poi.ImageThumbnail = PoiMediaUrls.ResolveThumbnail(Request, poi.ImageThumbnail);
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
    }
}
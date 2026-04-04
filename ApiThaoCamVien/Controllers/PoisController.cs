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
        private readonly WebContext _context;

        public PoisController(WebContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/Pois?lang=en
        /// Trả về danh sách POI với tên và mô tả đã được dịch sang ngôn ngữ yêu cầu.
        /// Nếu không có bản dịch, fallback về tiếng Việt gốc trong bảng pois.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPois([FromQuery] string lang = "vi")
        {
            // Lấy tất cả POI đang active, sắp xếp theo priority
            var pois = await _context.Pois
                .Where(p => p.IsActive == true)
                .OrderByDescending(p => p.Priority)
                .ToListAsync();

            // Nếu không phải tiếng Việt, lấy bản dịch từ bảng poi_translations
            if (lang != "vi")
            {
                // Lấy tất cả bản dịch của ngôn ngữ yêu cầu trong 1 query duy nhất (tránh N+1)
                var translations = await _context.PoiTranslations
                    .Where(t => t.LanguageCode == lang)
                    .ToDictionaryAsync(t => t.PoiId);

                // Áp dụng bản dịch vào từng POI
                foreach (var poi in pois)
                {
                    if (translations.TryGetValue(poi.PoiId, out var trans))
                    {
                        // Chỉ ghi đè nếu bản dịch không trống
                        if (!string.IsNullOrWhiteSpace(trans.Name))
                            poi.Name = trans.Name;
                        if (!string.IsNullOrWhiteSpace(trans.Description))
                            poi.Description = trans.Description;
                    }
                    // Nếu không có bản dịch → giữ nguyên tiếng Việt gốc (fallback an toàn)
                }
            }

            return Ok(pois);
        }

        /// <summary>
        /// GET /api/Pois/5?lang=en
        /// Trả về chi tiết 1 POI đã được dịch.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPoi(int id, [FromQuery] string lang = "vi")
        {
            var poi = await _context.Pois.FindAsync(id);
            if (poi == null)
                return NotFound(new { message = "Không tìm thấy địa điểm này." });

            // Áp dụng bản dịch nếu cần
            if (lang != "vi")
            {
                var trans = await _context.PoiTranslations
                    .FirstOrDefaultAsync(t => t.PoiId == id && t.LanguageCode == lang);

                if (trans != null)
                {
                    if (!string.IsNullOrWhiteSpace(trans.Name))
                        poi.Name = trans.Name;
                    if (!string.IsNullOrWhiteSpace(trans.Description))
                        poi.Description = trans.Description;
                }
            }

            return Ok(poi);
        }

        /// <summary>
        /// GET /api/Pois/qr/TCVN-008?lang=en
        /// Tìm POI theo mã QR và trả về đã được dịch.
        /// Dùng cho QrPage khi người dùng quét mã QR.
        /// </summary>
        [HttpGet("qr/{qrData}")]
        public async Task<IActionResult> GetPoiByQr(string qrData, [FromQuery] string lang = "vi")
        {
            var qrCode = await _context.QrCodes
                .FirstOrDefaultAsync(q => q.QrCodeData == qrData);

            if (qrCode == null)
                return NotFound(new { message = $"Mã QR '{qrData}' chưa được đăng ký." });

            // Tái sử dụng logic GetPoi để có dịch thuật nhất quán
            return await GetPoi(qrCode.PoiId, lang);
        }

        /// <summary>
        /// GET /api/Pois/numpad/8?lang=en
        /// Tìm POI theo PoiId (người dùng nhập số trên NumpadPage).
        /// </summary>
        [HttpGet("numpad/{poiId}")]
        public async Task<IActionResult> GetPoiByNumpad(int poiId, [FromQuery] string lang = "vi")
            => await GetPoi(poiId, lang);
    }
}

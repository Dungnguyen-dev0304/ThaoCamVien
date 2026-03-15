using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models; // Kết nối tới folder Shared của Dương

namespace ApiThaoCamVien.Models// Kiểm tra lại namespace của project API của bạn
{
    [Route("api/[controller]")]
    [ApiController]
    public class MapController : ControllerBase
    {
        private readonly WebContext _context; // Thay bằng tên DbContext thật của bạn

        public MapController(WebContext context)
        {
            _context = context;
        }

        // Đường dẫn: GET https://localhost:xxxx/api/map/pois
        [HttpGet("pois")]
        public async Task<ActionResult<IEnumerable<Poi>>> GetPois()
        {
            // Lấy toàn bộ danh sách chuồng thú từ SQL
            var pois = await _context.Pois.ToListAsync();

            if (pois == null || !pois.Any())
            {
                return NotFound("Không tìm thấy dữ liệu địa điểm nào.");
            }

            return Ok(pois);
        }
    }
}
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
        // Sử dụng WebContext đã cấu hình lại để khớp với Database SQL Server
        private readonly WebContext _context;

        public PoisController(WebContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPois()
        {
            // Lấy danh sách địa điểm trong Thảo Cầm Viên
            // Đã lược bỏ .Include vì Model Poi.cs hiện tại là bản phẳng (SQLite)
            var data = await _context.Pois
                .Where(p => p.IsActive == true)
                .OrderByDescending(p => p.Priority)
                .ToListAsync();

            return Ok(data);
        }

        // Thêm hàm lấy chi tiết 1 địa điểm để App Android hiển thị thông tin khi nhấn vào Marker
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPoi(int id)
        {
            var poi = await _context.Pois.FindAsync(id);

            if (poi == null)
            {
                return NotFound(new { message = "Không tìm thấy địa điểm này trong Thảo Cầm Viên." });
            }

            return Ok(poi);
        }
    }
}
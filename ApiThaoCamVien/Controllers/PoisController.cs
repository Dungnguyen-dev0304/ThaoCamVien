using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiThaoCamVien.Models;

namespace ApiThaoCamVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PoisController : ControllerBase
    {
        // Đã đổi từ ApplicationDbContext thành WebContext
        private readonly WebContext _context;

        public PoisController(WebContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPois()
        {
            // Kiểm tra tên bảng: EF Core thường tự thêm "s" hoặc giữ nguyên. 
            // Nếu lỗi ở ".Pois", bạn thử đổi thành ".Pois" (viết hoa) hoặc ".pois" (viết thường)
            var data = await _context.Pois
                .Include(p => p.PoiMedia)
                .Where(p => p.IsActive == true)
                .OrderByDescending(p => p.Priority)
                .ToListAsync();

            return Ok(data);
        }
    }
}
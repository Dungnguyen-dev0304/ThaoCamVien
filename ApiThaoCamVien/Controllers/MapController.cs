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

        // Tọa độ ranh giới Thao Cam Vien
        [HttpGet("boundary")]
        public IActionResult GetBoundary()
        {
            // Danh sách tọa độ Dương cung cấp (đã đảo ngược Lat-Lng)
            var boundary = new[]
            {
        new { lat = 10.78724457565717, lng = 106.70905874088123 },
        new { lat = 10.7873027730829, lng = 106.70890618380582 },
        new { lat = 10.788037496812976, lng = 106.70798948298363 },
        new { lat = 10.788896811692808, lng = 106.70734526765654 },
        new { lat = 10.789030038586532, lng = 106.70731136158673 },
        new { lat = 10.7905554823067, lng = 106.70679598932571 },
        new { lat = 10.79048886920556, lng = 106.706714614757 },
        new { lat = 10.790382288213152, lng = 106.706714614757 },
        new { lat = 10.790255723234608, lng = 106.70647049105412 },
        new { lat = 10.790422256089656, lng = 106.70618568006836 },
        new { lat = 10.790448901337939, lng = 106.70592799393785 },
        new { lat = 10.7906620632384, lng = 106.70569743266253 },
        new { lat = 10.790522175757985, lng = 106.70557537081106 },
        new { lat = 10.790615434085268, lng = 106.70552112109908 },
        new { lat = 10.78883931702984, lng = 106.7038511695821 },
        new { lat = 10.788197319227706, lng = 106.7045187738405 },
        new { lat = 10.787990222869666, lng = 106.70432903368362 },
        new { lat = 10.784975501396715, lng = 106.70767270730829 },
        new { lat = 10.785048666985617, lng = 106.70775960205157 },
        new { lat = 10.784951112862245, lng = 106.70787132386351 },
        new { lat = 10.785158415336312, lng = 106.70798304567552 },
        new { lat = 10.785101053220643, lng = 106.70815528553214 },
        new { lat = 10.78724457565717, lng = 106.70905874088123 }
    };

            return Ok(boundary);
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
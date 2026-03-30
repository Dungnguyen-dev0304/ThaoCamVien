using Microsoft.AspNetCore.Mvc;
using ApiThaoCamVien.Models;
using SharedThaoCamVien.Models;
using System.Threading.Tasks;

namespace WebThaoCamVien.Controllers
{
    public class AdminController : Controller
    {
        private readonly WebContext _context;

        // 3. Tiêm WebContext vào Controller để sử dụng
        public AdminController(WebContext context)
        {
            _context = context;
        }

        // GET: /admin/index
        public IActionResult Index()
        {
            return View();
        }

        // GET: /admin/AddPOI (Hiển thị trang thêm mới)
        public IActionResult AddPOI()
        {
            return View();
        }

        // 4. POST: /admin/AddPOI (Hàm này sẽ chạy khi bạn nhấn nút "Lưu")
        [HttpPost]
        public async Task<IActionResult> AddPOI(Poi model)
        {
            // Kiểm tra nếu dữ liệu hợp lệ (không trống tên, đúng định dạng...)
            if (ModelState.IsValid)
            {
                model.CreatedAt = System.DateTime.Now; // Gán ngày tạo tự động
                model.IsActive = true; // Mặc định cho hiển thị

                _context.Pois.Add(model); // Thêm vào bộ nhớ tạm
                await _context.SaveChangesAsync(); // Lưu thực sự xuống SQLite

                // Sau khi lưu xong, chuyển hướng về trang Index
                return RedirectToAction("Index");
            }

            // Nếu có lỗi, trả lại View kèm dữ liệu đã nhập để người dùng sửa
            return View(model);
        }
    }
}
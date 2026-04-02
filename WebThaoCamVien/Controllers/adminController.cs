using Microsoft.AspNetCore.Mvc;
using ApiThaoCamVien.Models;
using SharedThaoCamVien.Models;
using Microsoft.EntityFrameworkCore;

namespace WebThaoCamVien.Controllers
{
    public class AdminController : Controller
    {
        private readonly WebContext _context;
        private readonly IWebHostEnvironment _env;


        private static readonly (double Lat, double Lng)[] BoundaryPolygon =
        {
            (10.790530958743389, 106.70681254947431),
            (10.790456999508692, 106.70671698845888),
            (10.790257700153376, 106.70651554322427),
            (10.79042812075069,  106.70628133432854),
            (10.790440902291763, 106.70618591588868),
            (10.79041107869567,  106.70598640460645),
            (10.790483507424142, 106.70586496295732),
            (10.790658188404294, 106.70571749809534),
            (10.790530373063206, 106.70559171924418),
            (10.790564457159661, 106.70547895199752),
            (10.788817019051919, 106.70383791235048),
            (10.788177937888548, 106.70454487624346),
            (10.787977692177435, 106.7043323533548),
            (10.784948471879815, 106.70767588372479),
            (10.785033683695005, 106.70775395335602),
            (10.78498255660898,  106.70786672060262),
            (10.785157240784699, 106.70799683665604),
            (10.785072029004596, 106.70815731312257),
            (10.787234071848147, 106.70907826576183),
            (10.78740363601608,  106.70870791908663),
            (10.787693353724208, 106.70829154771366),
            (10.788434688943752, 106.70767132785807),
            (10.788928911406757, 106.70735037492676),
            (10.790530958743389, 106.70681254947431),
        };

        private const double MinDistanceMeters = 5.0;



        public AdminController(WebContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private static bool IsInsideBoundary(double lat, double lng)
        {
            bool inside = false;
            int n = BoundaryPolygon.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = BoundaryPolygon[i].Lat, yi = BoundaryPolygon[i].Lng;
                double xj = BoundaryPolygon[j].Lat, yj = BoundaryPolygon[j].Lng;
                bool intersect = ((yi > lng) != (yj > lng)) &&
                                 (lat < (xj - xi) * (lng - yi) / (yj - yi) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private static double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            const double R = 6371000;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLng = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private void SetViewData(string active, string title, string pageTitle)
        {
            ViewData["Active"] = active;
            ViewData["Title"] = title;
            ViewData["PageTitle"] = pageTitle;
        }

        // GET: /admin/index
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Admin/PoiList
        public async Task<IActionResult> PoiList()
        {
            ViewData["Active"] = "poilist";
            ViewData["Title"] = "Danh sách địa điểm";
            ViewData["PageTitle"] = "Quản lý điểm tham quan";

            var list = await _context.Pois.OrderByDescending(p => p.CreatedAt).ToListAsync();
            return View(list);
        }

        // ── ADD POI ──────────────────────────────────────────────────────

        public IActionResult AddPOI()
        {
            SetViewData("addpoi", "Thêm POI", "Quản lý điểm tham quan");
            return View(new Poi());
        }

        // POST: /admin/AddPOI
        [HttpPost]
        public async Task<IActionResult> AddPOI(Poi model, IFormFile? imageFile)
        {
            ModelState.Remove("ImageThumbnail");

            ViewData["Active"] = "addpoi";
            ViewData["Title"] = "Thêm POI";
            ViewData["PageTitle"] = "Quản lý điểm tham quan";

            // KIỂM TRA THỦ CÔNG: Bắt buộc phải có ảnh khi thêm mới
            if (imageFile == null || imageFile.Length == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn hình ảnh đại diện cho địa điểm.");
            }

            if (!ModelState.IsValid)
                return View(model);

            double lat = (double)model.Latitude;
            double lng = (double)model.Longitude;

            // Validation 1: Kiểm tra nằm trong ranh giới Thảo Cầm Viên
            if (!IsInsideBoundary(lat, lng))
            {
                ModelState.AddModelError("", "Vị trí được chọn nằm ngoài ranh giới Thảo Cầm Viên. Vui lòng chọn lại.");
                return View(model);
            }

            // Validation 2: Kiểm tra trùng vị trí với POI đã có trong database
            var existingPois = await _context.Pois.ToListAsync();
            var duplicate = existingPois.FirstOrDefault(p =>
                CalculateDistance(lat, lng, (double)p.Latitude, (double)p.Longitude) < MinDistanceMeters);

            if (duplicate != null)
            {
                ModelState.AddModelError("", $"Vị trí này quá gần với POI đã tồn tại: \"{duplicate.Name}\" (cách {MinDistanceMeters}m). Vui lòng chọn vị trí khác.");
                return View(model);
            }

            // Xử lý upload ảnh
            if (imageFile != null && imageFile.Length > 0)
            {
                string originalName = Path.GetFileNameWithoutExtension(imageFile.FileName);
                string extension = Path.GetExtension(imageFile.FileName);
                string fileName = originalName + extension;

                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "pois");
                Directory.CreateDirectory(uploadPath);

                string filePath = Path.Combine(uploadPath, fileName);
                int counter = 1;
                while (System.IO.File.Exists(filePath))
                {
                    fileName = $"{originalName}_{counter}{extension}";
                    filePath = Path.Combine(uploadPath, fileName);
                    counter++;
                }

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                model.ImageThumbnail = fileName;
            }

            model.CreatedAt = DateTime.Now;
            model.IsActive = true;

            _context.Pois.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("AddPOI");
        }

        // ── EDIT POI ─────────────────────────────────────────────────────

        public async Task<IActionResult> EditPOI(int id)
        {
            SetViewData("poilist", "Chỉnh sửa POI", "Quản lý điểm tham quan");
            var poi = await _context.Pois.FindAsync(id);
            if (poi == null) return NotFound();
            return View(poi);
        }

        [HttpPost]
        public async Task<IActionResult> EditPOI(Poi model, IFormFile? imageFile)
        {
            ModelState.Remove("ImageThumbnail");
            SetViewData("poilist", "Chỉnh sửa POI", "Quản lý điểm tham quan");

            if (!ModelState.IsValid) return View(model);

            double lat = (double)model.Latitude;
            double lng = (double)model.Longitude;

            if (!IsInsideBoundary(lat, lng))
            {
                ModelState.AddModelError("", "Vị trí được chọn nằm ngoài ranh giới Thảo Cầm Viên. Vui lòng chọn lại.");
                return View(model);
            }

            // Bỏ qua chính POI đang sửa khi kiểm tra trùng vị trí
            var existingPois = await _context.Pois.Where(p => p.PoiId != model.PoiId).ToListAsync();
            var duplicate = existingPois.FirstOrDefault(p =>
                CalculateDistance(lat, lng, (double)p.Latitude, (double)p.Longitude) < MinDistanceMeters);

            if (duplicate != null)
            {
                ModelState.AddModelError("", $"Vị trí này quá gần với POI đã tồn tại: \"{duplicate.Name}\" (trong vòng {MinDistanceMeters}m). Vui lòng chọn vị trí khác.");
                return View(model);
            }

            var existing = await _context.Pois.FindAsync(model.PoiId);
            if (existing == null) return NotFound();

            existing.Name = model.Name;
            existing.CategoryId = model.CategoryId;
            existing.Latitude = model.Latitude;
            existing.Longitude = model.Longitude;
            existing.Description = model.Description;

            if (imageFile != null && imageFile.Length > 0)
            {
                // Xóa ảnh cũ trước khi lưu ảnh mới
                if (!string.IsNullOrEmpty(existing.ImageThumbnail))
                {
                    string oldPath = Path.Combine(_env.WebRootPath, "images", "pois", existing.ImageThumbnail);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                await SaveImageAsync(imageFile, existing);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("PoiList");
        }

        // ── DELETE POI ───────────────────────────────────────────────────

        // GET: Hiển thị trang xác nhận
        public async Task<IActionResult> DeletePOI(int id)
        {
            SetViewData("poilist", "Xóa POI", "Quản lý điểm tham quan");
            var poi = await _context.Pois.FindAsync(id);
            if (poi == null) return NotFound();
            return View(poi);
        }

        // POST: Thực hiện xóa
        [HttpPost, ActionName("DeletePOI")]
        public async Task<IActionResult> DeletePOIConfirmed(int PoiId)
        {
            var poi = await _context.Pois.FindAsync(PoiId);
            if (poi == null) return NotFound();

            if (!string.IsNullOrEmpty(poi.ImageThumbnail))
            {
                string imagePath = Path.Combine(_env.WebRootPath, "images", "pois", poi.ImageThumbnail);
                if (System.IO.File.Exists(imagePath))
                    System.IO.File.Delete(imagePath);
            }

            _context.Pois.Remove(poi);
            await _context.SaveChangesAsync();
            return RedirectToAction("PoiList");
        }

        // ── HELPER: LƯU ẢNH ─────────────────────────────────────────────

        private async Task SaveImageAsync(IFormFile imageFile, Poi model)
        {
            if (imageFile == null || imageFile.Length == 0) return;

            string originalName = Path.GetFileNameWithoutExtension(imageFile.FileName);
            string extension = Path.GetExtension(imageFile.FileName);
            string fileName = originalName + extension;

            string uploadPath = Path.Combine(_env.WebRootPath, "images", "pois");
            Directory.CreateDirectory(uploadPath);

            string filePath = Path.Combine(uploadPath, fileName);
            int counter = 1;
            while (System.IO.File.Exists(filePath))
            {
                fileName = $"{originalName}_{counter}{extension}";
                filePath = Path.Combine(uploadPath, fileName);
                counter++;
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            model.ImageThumbnail = fileName;
        }
    }
}
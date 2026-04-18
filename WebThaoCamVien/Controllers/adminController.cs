using ApiThaoCamVien.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;
using WebThaoCamVien.ViewModels;

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
        public async Task<IActionResult> Index(int? poiId, int days = 7, int page = 1)
        {
            SetViewData("index", "Tổng quan", "Lịch sử sử dụng");

            const int pageSize = 20;

            var query = _context.PoiVisitHistories
                .Include(v => v.Poi)
                .AsQueryable();

            if (poiId.HasValue)
                query = query.Where(v => v.PoiId == poiId);

            if (days > 0)
            {
                var from = DateTime.Now.AddDays(-days);
                query = query.Where(v => v.VisitTime >= from);
            }

            var today = DateTime.Today;
            var allVisits = await _context.PoiVisitHistories.ToListAsync();

            int totalVisits = await query.CountAsync();
            int todayVisits = allVisits.Count(v => v.VisitTime?.Date == today);
            // Bảng Users đã bị bỏ — giữ field trong ViewModel cho tương thích view cũ.
            int totalUsers = 0;
            int avgListen = allVisits.Any(v => v.ListenDuration.HasValue)
                                ? (int)allVisits.Where(v => v.ListenDuration.HasValue).Average(v => v.ListenDuration!.Value)
                                : 0;

            var last7Days = new List<DayVisitData>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var count = allVisits.Count(v => v.VisitTime?.Date == date);
                last7Days.Add(new DayVisitData { Date = date, Count = count });
            }

            var topPois = await _context.PoiVisitHistories
                .Include(v => v.Poi)
                .Where(v => v.Poi != null)
                .GroupBy(v => new { v.PoiId, v.Poi!.Name, v.Poi.CategoryId })
                .Select(g => new TopPoiData
                {
                    PoiName = g.Key.Name,
                    CategoryId = g.Key.CategoryId,
                    VisitCount = g.Count()
                })
                .OrderByDescending(x => x.VisitCount)
                .Take(5)
                .ToListAsync();

            int totalPages = (int)Math.Ceiling(totalVisits / (double)pageSize);
            var recentVisits = await query
                .OrderByDescending(v => v.VisitTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(v => new VisitRowData
                {
                    VisitTime = v.VisitTime,
                    DisplayName = null, // Bỏ — không còn liên kết user
                    Email = null,        // Bỏ — không còn liên kết user
                    PoiName = v.Poi != null ? v.Poi.Name : null,
                    CategoryId = v.Poi != null ? v.Poi.CategoryId : null,
                    ListenDuration = v.ListenDuration
                })
                .ToListAsync();

            var allPois = await _context.Pois.OrderBy(p => p.Name).ToListAsync();

            var activeSince = DateTime.UtcNow.AddSeconds(-90);
            var activeAppSessions = await _context.AppClientPresences
                .AsNoTracking()
                .CountAsync(p => p.LastSeenUtc >= activeSince);

            var viewModel = new IndexViewModel
            {
                TotalVisits = totalVisits,
                TodayVisits = todayVisits,
                TotalUsers = totalUsers,
                AvgListenDuration = avgListen,
                ActiveAppSessionsNow = activeAppSessions,
                Last7Days = last7Days,
                TopPois = topPois,
                RecentVisits = recentVisits,
                AllPois = allPois,
                FilterPoiId = poiId,
                FilterDays = days,
                CurrentPage = page,
                TotalPages = Math.Max(totalPages, 1)
            };

            return View(viewModel);
        }

        /// <summary>JSON for dashboard polling: app sessions seen recently (devices need internet to ping API).</summary>
        [HttpGet]
        public async Task<IActionResult> ActiveAppSessionsJson([FromQuery] int staleSeconds = 90)
        {
            staleSeconds = Math.Clamp(staleSeconds, 30, 600);
            var since = DateTime.UtcNow.AddSeconds(-staleSeconds);
            var count = await _context.AppClientPresences.AsNoTracking()
                .CountAsync(p => p.LastSeenUtc >= since);
            return Json(new { activeCount = count, staleSeconds, updatedAtUtc = DateTime.UtcNow });
        }

        // ─────────────────────────────────────────────────────────────────
        // HEATMAP: bản đồ nhiệt mật độ thăm quan POI
        // Nguồn dữ liệu:
        //   • poi_visit_history JOIN pois — mỗi POI là 1 điểm, trọng số = số lượt thăm
        //     trong khoảng [now - days, now]
        //   • (Tuỳ chọn) user_location_log — điểm GPS thô của thiết bị (chưa có
        //     controller nào đang ghi vào bảng này, sẽ rỗng cho tới khi bạn
        //     thêm endpoint thu log vị trí từ app)
        // ─────────────────────────────────────────────────────────────────
        public IActionResult Heatmap()
        {
            SetViewData("heatmap", "Bản đồ nhiệt", "Mật độ thăm quan");
            return View();
        }

        /// <summary>JSON cho heatmap: [{ lat, lng, weight, name }, ...]</summary>
        [HttpGet]
        public async Task<IActionResult> HeatmapDataJson(
            [FromQuery] int days = 30,
            [FromQuery] string source = "visit")
        {
            days = Math.Clamp(days, 1, 365);
            var fromDate = DateTime.Now.AddDays(-days);

            if (string.Equals(source, "location", StringComparison.OrdinalIgnoreCase))
            {
                // Nguồn: user_location_log (toạ độ GPS thô từ app)
                var raw = await _context.UserLocationLogs.AsNoTracking()
                    .Where(l => l.Latitude != null && l.Longitude != null
                                && l.RecordedAt != null && l.RecordedAt >= fromDate)
                    .Select(l => new { l.Latitude, l.Longitude })
                    .ToListAsync();

                var logs = raw.Select(l => new
                {
                    lat = (double)l.Latitude!.Value,
                    lng = (double)l.Longitude!.Value,
                    weight = 1.0,
                    name = (string?)null
                }).ToList();

                return Json(new
                {
                    source = "location",
                    days,
                    totalPoints = logs.Count,
                    totalWeight = logs.Count,
                    points = logs
                });
            }

            // Mặc định: poi_visit_history (số lượt xem / nghe theo từng POI)
            // GroupBy PoiId ở SQL rồi JOIN client-side với Pois để lấy lat/lng/name
            // (tránh EF cố translate cast sang double trong GroupBy).
            var visitGroups = await _context.PoiVisitHistories.AsNoTracking()
                .Where(v => v.VisitTime != null && v.VisitTime >= fromDate && v.PoiId != null)
                .GroupBy(v => v.PoiId!.Value)
                .Select(g => new { PoiId = g.Key, Count = g.Count() })
                .ToListAsync();

            var poiIds = visitGroups.Select(g => g.PoiId).ToList();
            var poiLookup = await _context.Pois.AsNoTracking()
                .Where(p => poiIds.Contains(p.PoiId))
                .Select(p => new { p.PoiId, p.Name, p.Latitude, p.Longitude })
                .ToListAsync();

            var points = visitGroups
                .Join(poiLookup, g => g.PoiId, p => p.PoiId, (g, p) => new
                {
                    lat = (double)p.Latitude,
                    lng = (double)p.Longitude,
                    weight = (double)g.Count,
                    name = p.Name
                })
                .ToList();

            return Json(new
            {
                source = "visit",
                days,
                totalPoints = points.Count,
                totalWeight = points.Sum(x => x.weight),
                points
            });
        }

        // GET: /Admin/PoiList
        public async Task<IActionResult> PoiList()
        {
            SetViewData("poilist", "Danh sách địa điểm", "Quản lý điểm tham quan");
            var list = await _context.Pois.OrderByDescending(p => p.CreatedAt).ToListAsync();
            ViewBag.NewPoi = new Poi();
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

            if (imageFile == null || imageFile.Length == 0)
            {
                ModelState.AddModelError("", "Vui lòng chọn hình ảnh đại diện cho địa điểm.");
            }

            if (!ModelState.IsValid)
                return View(model);

            double lat = (double)model.Latitude;
            double lng = (double)model.Longitude;

            if (!IsInsideBoundary(lat, lng))
            {
                ModelState.AddModelError("", "Vị trí được chọn nằm ngoài ranh giới Thảo Cầm Viên. Vui lòng chọn lại.");
                return View(model);
            }

            var existingPois = await _context.Pois.ToListAsync();
            var duplicate = existingPois.FirstOrDefault(p =>
                CalculateDistance(lat, lng, (double)p.Latitude, (double)p.Longitude) < MinDistanceMeters);

            if (duplicate != null)
            {
                ModelState.AddModelError("", $"Vị trí này quá gần với POI đã tồn tại: \"{duplicate.Name}\" (cách {MinDistanceMeters}m). Vui lòng chọn vị trí khác.");
                return View(model);
            }

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

            return RedirectToAction("PoiList");
        }

        // ── EDIT POI ─────────────────────────────────────────────────────

        // GET: /Admin/EditPOI?id=1
        public async Task<IActionResult> EditPOI(int id)
        {
            SetViewData("poilist", "Chỉnh sửa POI", "Quản lý điểm tham quan");
            var poi = await _context.Pois.FindAsync(id);
            if (poi == null) return NotFound();

            // Chỉ trả về thông tin POI, không còn load translations
            return View(new EditPOIViewModel { Poi = poi, Translations = new List<PoiTranslation>() });
        }

        // POST: /Admin/EditPOI
        [HttpPost]
        public async Task<IActionResult> EditPOI(EditPOIViewModel viewModel, IFormFile? imageFile)
        {
            var model = viewModel.Poi;
            ModelState.Remove("Poi.ImageThumbnail");
            SetViewData("poilist", "Chỉnh sửa POI", "Quản lý điểm tham quan");

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            double lat = (double)model.Latitude;
            double lng = (double)model.Longitude;

            if (!IsInsideBoundary(lat, lng))
            {
                ModelState.AddModelError("", "Vị trí được chọn nằm ngoài ranh giới Thảo Cầm Viên. Vui lòng chọn lại.");
                return View(viewModel);
            }

            var existingPois = await _context.Pois.Where(p => p.PoiId != model.PoiId).ToListAsync();
            var duplicate = existingPois.FirstOrDefault(p =>
                CalculateDistance(lat, lng, (double)p.Latitude, (double)p.Longitude) < MinDistanceMeters);

            if (duplicate != null)
            {
                ModelState.AddModelError("", $"Vị trí này quá gần với POI \"{duplicate.Name}\" (trong vòng {MinDistanceMeters}m).");
                return View(viewModel);
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
                if (!string.IsNullOrEmpty(existing.ImageThumbnail))
                {
                    string oldPath = Path.Combine(_env.WebRootPath, "images", "pois", existing.ImageThumbnail);
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }
                await SaveImageAsync(imageFile, existing);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("PoiList");
        }

        // ── DELETE POI ────────────────────────────────────────────────────

        public async Task<IActionResult> DeletePOI(int id)
        {
            SetViewData("poilist", "Xóa POI", "Quản lý điểm tham quan");
            var poi = await _context.Pois.FindAsync(id);
            if (poi == null) return NotFound();
            return View(poi);
        }

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
using Microsoft.AspNetCore.Mvc;
using ApiThaoCamVien.Models;
using SharedThaoCamVien.Models;
using Microsoft.EntityFrameworkCore;
using WebThaoCamVien.ViewModels;

namespace WebThaoCamVien.Controllers
{
    public class TourController : Controller
    {
        private readonly WebContext _context;

        public TourController(WebContext context)
        {
            _context = context;
        }

        private void SetViewData(string active, string title, string pageTitle)
        {
            ViewData["Active"] = active;
            ViewData["Title"] = title;
            ViewData["PageTitle"] = pageTitle;
        }

        // GET: /Tour/TourList
        public async Task<IActionResult> TourList()
        {
            SetViewData("tourlist", "Danh sách tour", "Quản lý Tour");
            var tours = await _context.Tours
                .Include(t => t.TourPois)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            return View(new TourListViewModel { Tours = tours });
        }

        // GET: /Tour/AddTour
        public async Task<IActionResult> AddTour()
        {
            SetViewData("tourlist", "Thêm tour", "Quản lý Tour");
            return View(new EditTourViewModel
            {
                AllPois = await _context.Pois.OrderBy(p => p.Name).ToListAsync()
            });
        }

        // POST: /Tour/AddTour
        [HttpPost]
        public async Task<IActionResult> AddTour(string Name, string? Description, int? EstimatedTime, List<int> PoiIds)
        {
            SetViewData("tourlist", "Thêm tour", "Quản lý Tour");

            if (string.IsNullOrWhiteSpace(Name))
            {
                ModelState.AddModelError("", "Vui lòng nhập tên tour.");
                return View(new EditTourViewModel
                {
                    AllPois = await _context.Pois.OrderBy(p => p.Name).ToListAsync()
                });
            }

            var tour = new Tour
            {
                Name = Name,
                Description = Description,
                EstimatedTime = EstimatedTime,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.Tours.Add(tour);
            await _context.SaveChangesAsync();

            for (int i = 0; i < PoiIds.Count; i++)
            {
                _context.TourPois.Add(new TourPoi
                {
                    TourId = tour.TourId,
                    PoiId = PoiIds[i],
                    OrderIndex = i + 1
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("TourList");
        }

        // GET: /Tour/EditTour?id=1
        public async Task<IActionResult> EditTour(int id)
        {
            SetViewData("tourlist", "Chỉnh sửa tour", "Quản lý Tour");

            var tour = await _context.Tours.FindAsync(id);
            if (tour == null) return NotFound();

            var tourPois = await _context.TourPois
                .Include(tp => tp.Poi)
                .Where(tp => tp.TourId == id)
                .OrderBy(tp => tp.OrderIndex)
                .ToListAsync();

            return View(new EditTourViewModel
            {
                Tour = tour,
                TourPois = tourPois,
                AllPois = await _context.Pois.OrderBy(p => p.Name).ToListAsync()
            });
        }

        // POST: /Tour/EditTour
        [HttpPost]
        public async Task<IActionResult> EditTour(int TourId, string Name, string? Description, int? EstimatedTime, List<int> PoiIds)
        {
            SetViewData("tourlist", "Chỉnh sửa tour", "Quản lý Tour");

            var tour = await _context.Tours.FindAsync(TourId);
            if (tour == null) return NotFound();

            tour.Name = Name;
            tour.Description = Description;
            tour.EstimatedTime = EstimatedTime;

            var oldStops = _context.TourPois.Where(tp => tp.TourId == TourId);
            _context.TourPois.RemoveRange(oldStops);
            await _context.SaveChangesAsync();

            for (int i = 0; i < PoiIds.Count; i++)
            {
                _context.TourPois.Add(new TourPoi
                {
                    TourId = TourId,
                    PoiId = PoiIds[i],
                    OrderIndex = i + 1
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("TourList");
        }

        // POST: /Tour/ToggleActive
        [HttpPost]
        public async Task<IActionResult> ToggleActive(int TourId)
        {
            var tour = await _context.Tours.FindAsync(TourId);
            if (tour == null) return NotFound();

            tour.IsActive = !tour.IsActive;
            await _context.SaveChangesAsync();
            return RedirectToAction("TourList");
        }

        // POST: /Tour/DeleteTour
        [HttpPost]
        public async Task<IActionResult> DeleteTour(int TourId)
        {
            var tour = await _context.Tours.FindAsync(TourId);
            if (tour == null) return NotFound();

            var stops = _context.TourPois.Where(tp => tp.TourId == TourId);
            _context.TourPois.RemoveRange(stops);
            _context.Tours.Remove(tour);

            await _context.SaveChangesAsync();
            return RedirectToAction("TourList");
        }
    }
}
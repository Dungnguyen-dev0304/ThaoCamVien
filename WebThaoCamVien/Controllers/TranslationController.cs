using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;
using ApiThaoCamVien.Models;

namespace WebThaoCamVien.Controllers
{
    [Authorize]
    public class TranslationController : Controller
    {
        private readonly WebContext _context;

        public TranslationController(WebContext context)
        {
            _context = context;
        }

        private void SetViewData(string active, string title, string pageTitle)
        {
            ViewData["Active"] = active;
            ViewData["Title"] = title;
            ViewData["PageTitle"] = pageTitle;
        }

        // GET: /Translation/Index
        public async Task<IActionResult> Index()
        {
            SetViewData("translation", "Quản lý bản dịch", "Quản lý bản dịch");

            // Lấy danh sách tất cả POI kèm số lượng bản dịch hiện có
            var pois = await _context.Pois
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var translationCounts = await _context.PoiTranslations
                .GroupBy(t => t.PoiId)
                .Select(g => new { PoiId = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.TranslationCounts = translationCounts.ToDictionary(x => x.PoiId, x => x.Count);

            return View(pois);
        }

        // GET: /Translation/EditTranslations?poiId=1
        [Route("Translation/EditTranslations/{poiId}")]
        public async Task<IActionResult> EditTranslations(int poiId)
        {
            SetViewData("translation", "Chỉnh sửa bản dịch", "Quản lý bản dịch");

            var poi = await _context.Pois.FindAsync(poiId);
            if (poi == null) return NotFound();

            var translations = await _context.PoiTranslations
                .Where(t => t.PoiId == poiId)
                .ToListAsync();

            ViewBag.Poi = poi;
            return View(translations);
        }

        // POST: /Translation/Save
        [HttpPost]
        public async Task<IActionResult> Save(int TranslationId, int PoiId, string LanguageCode, string Name, string Description)
        {
            if (TranslationId == 0)
            {
                // Kiểm tra trùng ngôn ngữ
                bool exists = await _context.PoiTranslations
                    .AnyAsync(t => t.PoiId == PoiId && t.LanguageCode == LanguageCode);

                if (exists)
                {
                    TempData["Error"] = $"Bản dịch ngôn ngữ '{LanguageCode}' cho địa điểm này đã tồn tại.";
                    return RedirectToAction("EditTranslations", new { poiId = PoiId });
                }

                var translation = new PoiTranslation
                {
                    PoiId = PoiId,
                    LanguageCode = LanguageCode,
                    Name = Name,
                    Description = Description
                };
                _context.PoiTranslations.Add(translation);
            }
            else
            {
                var existing = await _context.PoiTranslations.FindAsync(TranslationId);
                if (existing != null)
                {
                    existing.Name = Name;
                    existing.Description = Description;
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Lưu bản dịch thành công!";
            return RedirectToAction("EditTranslations", new { poiId = PoiId });
        }

        // POST: /Translation/Delete
        [HttpPost]
        public async Task<IActionResult> Delete(int TranslationId, int PoiId)
        {
            var translation = await _context.PoiTranslations.FindAsync(TranslationId);
            if (translation != null)
            {
                _context.PoiTranslations.Remove(translation);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa bản dịch.";
            }
            return RedirectToAction("EditTranslations", new { poiId = PoiId });
        }
    }
}
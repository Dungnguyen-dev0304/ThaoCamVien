using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ApiThaoCamVien.Models;
using SharedThaoCamVien.Models;

namespace ApiThaoCamVien.Controllers
{
    public class PoiMediumsController : Controller
    {
        private readonly WebContext _context;

        public PoiMediumsController(WebContext context)
        {
            _context = context;
        }

        // GET: PoiMediums
        public async Task<IActionResult> Index()
        {
            var webContext = _context.PoiMedia.Include(p => p.Poi);
            return View(await webContext.ToListAsync());
        }

        // GET: PoiMediums/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poiMedium = await _context.PoiMedia
                .Include(p => p.Poi)
                .FirstOrDefaultAsync(m => m.MediaId == id);
            if (poiMedium == null)
            {
                return NotFound();
            }

            return View(poiMedium);
        }

        // GET: PoiMediums/Create
        public IActionResult Create()
        {
            ViewData["PoiId"] = new SelectList(_context.Pois, "PoiId", "Description");
            return View();
        }

        // POST: PoiMediums/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MediaId,PoiId,MediaType,MediaUrl,Language")] PoiMedium poiMedium)
        {
            if (ModelState.IsValid)
            {
                _context.Add(poiMedium);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PoiId"] = new SelectList(_context.Pois, "PoiId", "Description", poiMedium.PoiId);
            return View(poiMedium);
        }

        // GET: PoiMediums/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poiMedium = await _context.PoiMedia.FindAsync(id);
            if (poiMedium == null)
            {
                return NotFound();
            }
            ViewData["PoiId"] = new SelectList(_context.Pois, "PoiId", "Description", poiMedium.PoiId);
            return View(poiMedium);
        }

        // POST: PoiMediums/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MediaId,PoiId,MediaType,MediaUrl,Language")] PoiMedium poiMedium)
        {
            if (id != poiMedium.MediaId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(poiMedium);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PoiMediumExists(poiMedium.MediaId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["PoiId"] = new SelectList(_context.Pois, "PoiId", "Description", poiMedium.PoiId);
            return View(poiMedium);
        }

        // GET: PoiMediums/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poiMedium = await _context.PoiMedia
                .Include(p => p.Poi)
                .FirstOrDefaultAsync(m => m.MediaId == id);
            if (poiMedium == null)
            {
                return NotFound();
            }

            return View(poiMedium);
        }

        // POST: PoiMediums/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var poiMedium = await _context.PoiMedia.FindAsync(id);
            if (poiMedium != null)
            {
                _context.PoiMedia.Remove(poiMedium);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PoiMediumExists(int id)
        {
            return _context.PoiMedia.Any(e => e.MediaId == id);
        }
    }
}

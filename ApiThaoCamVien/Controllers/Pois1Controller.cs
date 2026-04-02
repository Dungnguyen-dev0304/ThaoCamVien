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
    public class Pois1Controller : Controller
    {
        private readonly WebContext _context;

        public Pois1Controller(WebContext context)
        {
            _context = context;
        }

        // GET: Pois1
        public async Task<IActionResult> Index()
        {
            return View(await _context.Pois.ToListAsync());
        }

        // GET: Pois1/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poi = await _context.Pois
                .FirstOrDefaultAsync(m => m.PoiId == id);
            if (poi == null)
            {
                return NotFound();
            }

            return View(poi);
        }

        // GET: Pois1/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Pois1/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PoiId,CategoryId,Name,Description,Latitude,Longitude,Radius,Priority,ImageThumbnail,IsActive,CreatedAt")] Poi poi)
        {
            if (ModelState.IsValid)
            {
                _context.Add(poi);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(poi);
        }

        // GET: Pois1/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poi = await _context.Pois.FindAsync(id);
            if (poi == null)
            {
                return NotFound();
            }
            return View(poi);
        }

        // POST: Pois1/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PoiId,CategoryId,Name,Description,Latitude,Longitude,Radius,Priority,ImageThumbnail,IsActive,CreatedAt")] Poi poi)
        {
            if (id != poi.PoiId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(poi);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PoiExists(poi.PoiId))
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
            return View(poi);
        }

        // GET: Pois1/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var poi = await _context.Pois
                .FirstOrDefaultAsync(m => m.PoiId == id);
            if (poi == null)
            {
                return NotFound();
            }

            return View(poi);
        }

        // POST: Pois1/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var poi = await _context.Pois.FindAsync(id);
            if (poi != null)
            {
                _context.Pois.Remove(poi);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PoiExists(int id)
        {
            return _context.Pois.Any(e => e.PoiId == id);
        }
    }
}

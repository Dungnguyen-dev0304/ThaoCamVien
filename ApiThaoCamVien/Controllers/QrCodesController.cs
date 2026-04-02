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
    public class QrCodesController : Controller
    {
        private readonly WebContext _context;

        public QrCodesController(WebContext context)
        {
            _context = context;
        }

        // GET: QrCodes
        public async Task<IActionResult> Index()
        {
            var webContext = _context.QrCodes.Include(q => q.Poi);
            return View(await webContext.ToListAsync());
        }

        // GET: QrCodes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var qrCode = await _context.QrCodes
                .Include(q => q.Poi)
                .FirstOrDefaultAsync(m => m.QrId == id);
            if (qrCode == null)
            {
                return NotFound();
            }

            return View(qrCode);
        }

        // GET: QrCodes/Create
        public IActionResult Create()
        {
            ViewData["PoiId"] = new SelectList(_context.Pois, "PoiId", "Description");
            return View();
        }

        // POST: QrCodes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("QrId,PoiId,QrCodeData,CreatedAt")] QrCode qrCode)
        {
            if (ModelState.IsValid)
            {
                _context.Add(qrCode);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PoiId"] = new SelectList(_context.Pois, "PoiId", "Description", qrCode.PoiId);
            return View(qrCode);
        }

        // GET: QrCodes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var qrCode = await _context.QrCodes.FindAsync(id);
            if (qrCode == null)
            {
                return NotFound();
            }
            ViewData["PoiId"] = new SelectList(_context.Pois, "PoiId", "Description", qrCode.PoiId);
            return View(qrCode);
        }

        // POST: QrCodes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("QrId,PoiId,QrCodeData,CreatedAt")] QrCode qrCode)
        {
            if (id != qrCode.QrId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(qrCode);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!QrCodeExists(qrCode.QrId))
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
            ViewData["PoiId"] = new SelectList(_context.Pois, "PoiId", "Description", qrCode.PoiId);
            return View(qrCode);
        }

        // GET: QrCodes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var qrCode = await _context.QrCodes
                .Include(q => q.Poi)
                .FirstOrDefaultAsync(m => m.QrId == id);
            if (qrCode == null)
            {
                return NotFound();
            }

            return View(qrCode);
        }

        // POST: QrCodes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var qrCode = await _context.QrCodes.FindAsync(id);
            if (qrCode != null)
            {
                _context.QrCodes.Remove(qrCode);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool QrCodeExists(int id)
        {
            return _context.QrCodes.Any(e => e.QrId == id);
        }
    }
}

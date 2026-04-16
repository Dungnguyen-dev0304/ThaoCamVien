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

        /// <summary>
        /// GET: QrCodes/PrintQr
        /// Trang in mã QR cho TẤT CẢ POI active. Mỗi QR chứa "TCV-{poiId}".
        /// Mở trên trình duyệt → Ctrl+P để in.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PrintQr()
        {
            var pois = await _context.Pois.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.PoiId)
                .Select(p => new { p.PoiId, p.Name })
                .ToListAsync();

            // Sinh HTML inline (không cần View riêng)
            var cards = string.Join("\n", pois.Select(p =>
            {
                var qrData = $"TCV-{p.PoiId}";
                var padded = p.PoiId.ToString().PadLeft(3, '0');
                return $@"
                <div class=""card"">
                    <div class=""poi-id"">#{p.PoiId}</div>
                    <div class=""qr-container"" id=""qr-{p.PoiId}""></div>
                    <div class=""name"">{System.Net.WebUtility.HtmlEncode(p.Name ?? "---")}</div>
                    <div class=""code-text"">{qrData}</div>
                    <div class=""hint"">Nhập số: {padded}</div>
                </div>";
            }));

            var qrScripts = string.Join("\n", pois.Select(p =>
                $@"new QRCode(document.getElementById('qr-{p.PoiId}'), {{
                    text: 'TCV-{p.PoiId}',
                    width: 150, height: 150,
                    colorDark: '#1B5E20', colorLight: '#ffffff',
                    correctLevel: QRCode.CorrectLevel.H
                }});"));

            var html = $@"<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <title>In Mã QR - Thảo Cầm Viên</title>
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js""></script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #f5f5f5; padding: 20px; }}
        h1 {{ text-align: center; color: #1B5E20; margin-bottom: 5px; }}
        .subtitle {{ text-align: center; color: #666; margin-bottom: 20px; font-size: 13px; }}
        .btn {{ display: inline-block; background: #2E7D32; color: white; border: none; padding: 10px 24px; border-radius: 8px; font-size: 14px; cursor: pointer; text-decoration: none; margin-bottom: 20px; }}
        .btn:hover {{ background: #1B5E20; }}
        .center {{ text-align: center; }}
        .grid {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 20px; max-width: 1200px; margin: 0 auto; }}
        .card {{ background: white; border-radius: 16px; padding: 20px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.08); border: 2px solid #E8F5E9; page-break-inside: avoid; }}
        .card .poi-id {{ display: inline-block; background: #E8F5E9; color: #2E7D32; font-weight: bold; font-size: 13px; padding: 3px 12px; border-radius: 12px; margin-bottom: 10px; }}
        .card .qr-container {{ display: flex; justify-content: center; margin: 10px 0; }}
        .card .name {{ font-size: 14px; font-weight: 600; color: #1a1a1a; margin-top: 8px; }}
        .card .code-text {{ font-size: 18px; font-weight: bold; color: #2E7D32; margin-top: 6px; letter-spacing: 2px; }}
        .card .hint {{ font-size: 11px; color: #999; margin-top: 4px; }}
        @media print {{
            body {{ background: white; padding: 10px; }}
            .no-print {{ display: none; }}
            .grid {{ gap: 15px; grid-template-columns: repeat(3, 1fr); }}
            .card {{ box-shadow: none; border: 1px solid #ccc; padding: 15px; }}
        }}
    </style>
</head>
<body>
    <h1>Thảo Cầm Viên - Mã QR ({pois.Count} điểm)</h1>
    <p class=""subtitle"">In và dán mã QR tại mỗi chuồng/khu vực. Khách quét bằng app để nghe thuyết minh.</p>
    <div class=""center no-print""><button class=""btn"" onclick=""window.print()"">In tất cả</button></div>
    <div class=""grid"">{cards}</div>
    <script>{qrScripts}</script>
</body>
</html>";

            return Content(html, "text/html");
        }
    }
}

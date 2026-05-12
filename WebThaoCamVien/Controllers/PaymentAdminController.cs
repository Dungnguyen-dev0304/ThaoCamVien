using ApiThaoCamVien.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;

namespace WebThaoCamVien.Controllers;

/// <summary>
/// Admin: Quản lý thanh toán VNPay.
///
/// Routes:
///   GET  /PaymentAdmin                    — Dashboard tổng quan
///   GET  /PaymentAdmin/Detail/{txnCode}   — Chi tiết giao dịch
///   GET  /PaymentAdmin/Pricing            — Cấu hình giá Premium
///   POST /PaymentAdmin/UpdatePricing      — Cập nhật giá + bật/tắt premium
/// </summary>
public class PaymentAdminController : Controller
{
    private readonly WebContext _db;

    public PaymentAdminController(WebContext db)
    {
        _db = db;
    }

    // ─── Dashboard ────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(string? status = null, string? q = null, int page = 1, int size = 20)
    {
        var today = DateTime.UtcNow.Date;
        var stats = new
        {
            TotalToday   = await _db.PaymentTransactions.CountAsync(t => t.CreatedAt >= today),
            SuccessToday = await _db.PaymentTransactions.CountAsync(t => t.CreatedAt >= today && t.Status == "success"),
            PendingToday = await _db.PaymentTransactions.CountAsync(t => t.CreatedAt >= today && (t.Status == "pending" || t.Status == "processing")),
            FailedToday  = await _db.PaymentTransactions.CountAsync(t => t.CreatedAt >= today && t.Status == "failed"),
            RevenueToday = await _db.PaymentTransactions
                              .Where(t => t.CreatedAt >= today && t.Status == "success")
                              .SumAsync(t => (decimal?)t.Amount) ?? 0,
            RevenueTotal = await _db.PaymentTransactions
                              .Where(t => t.Status == "success")
                              .SumAsync(t => (decimal?)t.Amount) ?? 0,
        };

        var qry = _db.PaymentTransactions
            .Include(t => t.Poi)
            .OrderByDescending(t => t.CreatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) qry = qry.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
            qry = qry.Where(t => t.TransactionCode.Contains(q) || t.DeviceId.Contains(q));

        var total = await qry.CountAsync();
        var items = await qry.Skip((page - 1) * size).Take(size).ToListAsync();

        ViewBag.Stats = stats;
        ViewBag.Status = status;
        ViewBag.Query = q;
        ViewBag.Page = page;
        ViewBag.Size = size;
        ViewBag.Total = total;
        ViewBag.TotalPages = (int)Math.Ceiling((double)total / size);
        return View(items);
    }

    // ─── Chi tiết giao dịch ────────────────────────────────────────────────
    public async Task<IActionResult> Detail(string txnCode)
    {
        var txn = await _db.PaymentTransactions
            .Include(t => t.Poi)
            .FirstOrDefaultAsync(t => t.TransactionCode == txnCode);
        if (txn == null) return NotFound();

        var access = await _db.PremiumAccesses
            .FirstOrDefaultAsync(a => a.TransactionId == txn.Id);
        ViewBag.Access = access;
        return View(txn);
    }

    // ─── Cấu hình giá Premium ──────────────────────────────────────────────
    public async Task<IActionResult> Pricing()
    {
        var pois = await _db.Pois
            .OrderBy(p => p.PoiId)
            .Select(p => new
            {
                p.PoiId, p.Name, p.IsPremium, p.PremiumPrice, p.IsActive
            })
            .ToListAsync();
        return View(pois);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePricing(int poiId, bool isPremium, decimal? price)
    {
        var poi = await _db.Pois.FindAsync(poiId);
        if (poi == null) return NotFound();

        poi.IsPremium = isPremium;
        poi.PremiumPrice = isPremium ? (price ?? 50000) : null;
        await _db.SaveChangesAsync();

        TempData["Msg"] = $"Đã cập nhật POI #{poiId}: {poi.Name}";
        return RedirectToAction(nameof(Pricing));
    }
}

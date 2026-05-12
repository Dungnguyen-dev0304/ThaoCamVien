using ApiThaoCamVien.Models;
using ApiThaoCamVien.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;
using System.Text.Json;

namespace ApiThaoCamVien.Controllers;

/// <summary>
/// Payment API — Thanh toán Premium qua MoMo Wallet (sandbox).
///
/// Endpoints:
///   GET  /api/payment/check/{poiId}            — POI có premium không
///   GET  /api/payment/access/{poiId}            — Device đã mua chưa
///   POST /api/payment/create                    — Tạo giao dịch + lấy MoMo payUrl/deeplink/QR
///   GET  /api/payment/status/{txnCode}          — Polling trạng thái
///   GET  /api/payment/pay/{txnCode}             — Redirect 302 sang MoMo
///   GET  /api/payment/momo-return               — Browser redirect sau thanh toán
///   POST /api/payment/momo-ipn                  — MoMo IPN webhook
///   GET  /api/payment/history                   — Lịch sử của device
///   GET  /api/payment/debug-config              — Debug config
/// </summary>
[ApiController]
[Route("api/payment")]
public class PaymentController : ControllerBase
{
    private readonly WebContext _db;
    private readonly MoMoService _momo;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(WebContext db, MoMoService momo, ILogger<PaymentController> logger)
    {
        _db = db;
        _momo = momo;
        _logger = logger;
    }

    // ─── 1. Check Premium ──────────────────────────────────────────────────

    [HttpGet("check/{poiId:int}")]
    public async Task<IActionResult> CheckPremium(int poiId)
    {
        var poi = await _db.Pois.FindAsync(poiId);
        if (poi == null) return NotFound(new { error = "POI không tồn tại" });

        return Ok(new
        {
            poiId,
            isPremium = poi.IsPremium,
            price = poi.PremiumPrice ?? 0,
            currency = "VND",
            name = poi.Name
        });
    }

    // ─── 2. Check Access ───────────────────────────────────────────────────

    [HttpGet("access/{poiId:int}")]
    public async Task<IActionResult> CheckAccess(int poiId, [FromQuery] string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest(new { error = "deviceId là bắt buộc" });

        var poi = await _db.Pois.FindAsync(poiId);
        if (poi == null) return NotFound(new { error = "POI không tồn tại" });

        if (!poi.IsPremium)
            return Ok(new { hasAccess = true, reason = "not_premium" });

        var hasAccess = await _db.PremiumAccesses.AnyAsync(a =>
            a.PoiId == poiId &&
            a.DeviceId == deviceId &&
            (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow));

        return Ok(new { hasAccess, poiId, deviceId, reason = hasAccess ? "purchased" : "not_purchased" });
    }

    // ─── 3. Create Transaction ─────────────────────────────────────────────

    [HttpPost("create")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreatePaymentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceId))
            return BadRequest(new { error = "deviceId là bắt buộc" });

        var poi = await _db.Pois.FindAsync(req.PoiId);
        if (poi == null) return NotFound(new { error = "POI không tồn tại" });
        if (!poi.IsPremium) return BadRequest(new { error = "POI không phải Premium" });

        var alreadyOwned = await _db.PremiumAccesses.AnyAsync(a =>
            a.PoiId == req.PoiId && a.DeviceId == req.DeviceId &&
            (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow));

        if (alreadyOwned)
            return Ok(new { alreadyOwned = true, message = "Bạn đã mua nội dung này rồi" });

        // Hủy pending cũ
        var oldPending = await _db.PaymentTransactions
            .Where(t => t.PoiId == req.PoiId && t.DeviceId == req.DeviceId && t.Status == "pending")
            .ToListAsync();
        foreach (var old in oldPending) { old.Status = "expired"; old.UpdatedAt = DateTime.UtcNow; }

        var amount = poi.PremiumPrice ?? 50000;
        var txnCode = GenerateTxnCode();
        var orderInfo = $"Thanh toan POI {req.PoiId}";

        // Gọi MoMo để tạo order
        var momoResult = await _momo.CreateOrderAsync(txnCode, (long)amount, orderInfo);
        if (!momoResult.Success)
        {
            _logger.LogError("MoMo create failed: {Msg}", momoResult.Message);
            return StatusCode(502, new { error = "MoMo từ chối tạo giao dịch", detail = momoResult.Message });
        }

        var qrExpiry = _momo.GetQrExpiry();
        var txn = new PaymentTransaction
        {
            TransactionCode = txnCode,
            PoiId = req.PoiId,
            SessionId = req.SessionId ?? string.Empty,
            DeviceId = req.DeviceId,
            Amount = amount,
            PaymentMethod = "momo",
            Status = "pending",
            VnPayUrl = momoResult.PayUrl,        // tái sử dụng cột này lưu MoMo payUrl
            QrExpiredAt = qrExpiry,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.PaymentTransactions.Add(txn);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created MoMo payment {TxnCode} for POI {PoiId}", txnCode, req.PoiId);

        return Ok(new
        {
            transactionCode = txnCode,
            vnpayUrl = momoResult.PayUrl,         // giữ tên field cũ để mobile app không phải đổi
            payUrl = momoResult.PayUrl,
            deeplink = momoResult.Deeplink,
            qrCodeUrl = momoResult.QrCodeUrl,
            qrExpiredAt = qrExpiry,
            amount,
            currency = "VND",
            status = "pending",
            poiName = poi.Name,
            paymentMethod = "momo"
        });
    }

    // ─── 4. Polling Status ─────────────────────────────────────────────────

    [HttpGet("status/{txnCode}")]
    public async Task<IActionResult> GetStatus(string txnCode)
    {
        var txn = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.TransactionCode == txnCode);
        if (txn == null) return NotFound(new { error = "Giao dịch không tồn tại" });

        if (txn.Status == "pending" && txn.QrExpiredAt.HasValue && txn.QrExpiredAt < DateTime.UtcNow)
        {
            txn.Status = "expired";
            txn.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            transactionCode = txn.TransactionCode,
            status = txn.Status,
            poiId = txn.PoiId,
            amount = txn.Amount,
            completedAt = txn.CompletedAt,
            failureReason = txn.FailureReason,
            qrExpiredAt = txn.QrExpiredAt,
            accessGranted = txn.Status == "success"
        });
    }

    // ─── 5. Redirect helper ────────────────────────────────────────────────

    [HttpGet("pay/{txnCode}")]
    public async Task<IActionResult> RedirectToMoMo(string txnCode)
    {
        var txn = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.TransactionCode == txnCode);
        if (txn == null) return NotFound("Giao dịch không tồn tại");
        if (string.IsNullOrEmpty(txn.VnPayUrl)) return BadRequest("Giao dịch không có URL thanh toán");
        if (txn.Status is "success" or "failed" or "expired")
            return BadRequest($"Giao dịch đã '{txn.Status}', không thanh toán lại được.");

        return Redirect(txn.VnPayUrl);
    }

    // ─── 6. MoMo IPN (POST application/json) ───────────────────────────────

    [HttpPost("momo-ipn")]
    public async Task<IActionResult> MoMoIpn([FromBody] JsonElement body)
    {
        try
        {
            if (!_momo.VerifyIpnSignature(body, out var resultCode, out var orderId))
                return Ok(new { resultCode = 99, message = "Invalid signature" });

            var txn = await _db.PaymentTransactions
                .FirstOrDefaultAsync(t => t.TransactionCode == orderId);
            if (txn == null) return Ok(new { resultCode = 1, message = "Transaction not found" });

            if (txn.Status is "success" or "failed")
                return Ok(new { resultCode = 0, message = "Already processed" });

            // Verify số tiền
            var ipnAmount = body.TryGetProperty("amount", out var a) ? a.GetInt64() : 0;
            if (ipnAmount != (long)txn.Amount)
                return Ok(new { resultCode = 4, message = "Amount mismatch" });

            var rawResp = body.GetRawText();
            var transId = body.TryGetProperty("transId", out var ti) ? ti.GetRawText() : "";

            if (resultCode == 0)
            {
                txn.Status = "success";
                txn.GatewayRef = transId;
                txn.CompletedAt = DateTime.UtcNow;

                _db.PremiumAccesses.Add(new PremiumAccess
                {
                    TransactionId = txn.Id,
                    PoiId = txn.PoiId,
                    SessionId = txn.SessionId,
                    DeviceId = txn.DeviceId,
                    GrantedAt = DateTime.UtcNow
                });

                _logger.LogInformation("Payment SUCCESS: {Txn}", orderId);
            }
            else
            {
                txn.Status = "failed";
                txn.FailureReason = $"MoMo resultCode={resultCode}";
                _logger.LogWarning("Payment FAILED: {Txn} code={Code}", orderId, resultCode);
            }

            txn.GatewayResponse = rawResp;
            txn.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { resultCode = 0, message = "Confirmed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MoMo IPN error");
            return Ok(new { resultCode = 99, message = "Internal error" });
        }
    }

    // ─── 7. MoMo Return ────────────────────────────────────────────────────

    [HttpGet("momo-return")]
    public IActionResult MoMoReturn()
    {
        var resultCode = Request.Query["resultCode"].FirstOrDefault() ?? "";
        var orderId = Request.Query["orderId"].FirstOrDefault() ?? "";
        var status = resultCode == "0" ? "success" : "failed";
        return Content(GenerateReturnHtml(orderId, status), "text/html; charset=utf-8");
    }

    // ─── 8. History ────────────────────────────────────────────────────────

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] string deviceId, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest(new { error = "deviceId là bắt buộc" });

        var txns = await _db.PaymentTransactions
            .Where(t => t.DeviceId == deviceId && t.Status != "expired")
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new { t.TransactionCode, t.PoiId, t.Amount, t.Status, t.PaymentMethod, t.CreatedAt, t.CompletedAt })
            .ToListAsync();

        return Ok(txns);
    }

    // ─── 9b. DEV ONLY — giả lập thanh toán thành công cho demo ─────────────

    /// <summary>
    /// GET /api/payment/dev-mark-success/{txnCode}
    /// Giả lập IPN thành công — chỉ dùng cho DEMO khi sandbox không trả tiền thật được.
    /// Đánh dấu giao dịch success + tạo PremiumAccess. App sẽ nhận trong 3 giây tiếp.
    /// </summary>
    [HttpGet("dev-mark-success/{txnCode}")]
    public async Task<IActionResult> DevMarkSuccess(string txnCode)
    {
        var txn = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.TransactionCode == txnCode);
        if (txn == null) return NotFound(new { error = "Giao dịch không tồn tại" });

        if (txn.Status == "success")
            return Ok(new { message = "Đã success từ trước", status = "success" });

        txn.Status = "success";
        txn.GatewayRef = "DEV-FORCE-" + Guid.NewGuid().ToString("N")[..8];
        txn.CompletedAt = DateTime.UtcNow;
        txn.UpdatedAt = DateTime.UtcNow;
        txn.GatewayResponse = "{\"forced\":true,\"reason\":\"dev-mark-success\"}";

        // Cấp quyền truy cập (idempotent)
        var hasAccess = await _db.PremiumAccesses.AnyAsync(a => a.TransactionId == txn.Id);
        if (!hasAccess)
        {
            _db.PremiumAccesses.Add(new PremiumAccess
            {
                TransactionId = txn.Id,
                PoiId = txn.PoiId,
                SessionId = txn.SessionId,
                DeviceId = txn.DeviceId,
                GrantedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogWarning("[DEV] Force-marked txn {Txn} as SUCCESS for demo purposes", txnCode);

        return Ok(new
        {
            message = "Đã đánh dấu thành công. App sẽ tự nhận trong 3 giây tiếp theo.",
            transactionCode = txnCode,
            status = "success",
            poiId = txn.PoiId
        });
    }

    // ─── 9. Debug Config ───────────────────────────────────────────────────

    [HttpGet("debug-config")]
    public IActionResult DebugConfig()
    {
        var info = _momo.DebugInfo();
        return Ok(new
        {
            partnerCode = info.Partner,
            accessKey = info.AccessKey,
            secretKeyLength = info.SecretLen,
            endpoint = info.Endpoint,
            note = "secretKeyLength > 0 = config OK. PartnerCode 'MOMO' = sandbox demo."
        });
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static string GenerateTxnCode()
    {
        var date = DateTime.UtcNow.AddHours(7).ToString("yyyyMMdd");
        var rand = Guid.NewGuid().ToString("N")[..8].ToUpper();
        return $"TXN{date}{rand}";  // MoMo orderId không cho dấu '-' trong một số case → bỏ
    }

    private static string GenerateReturnHtml(string txnCode, string status)
    {
        var (icon, title, color, msg) = status == "success"
            ? ("✓", "Thanh toán thành công", "#0066cc", "Nội dung đã được mở khóa. Quay lại ứng dụng để tiếp tục.")
            : ("✕", "Thanh toán thất bại", "#d32f2f", "Có lỗi xảy ra. Vui lòng thử lại trong ứng dụng.");

        const string template = @"<!DOCTYPE html><html lang=""vi""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><title>Kết quả thanh toán · Thảo Cầm Viên</title><style>
* { margin:0; padding:0; box-sizing:border-box; }
body { font-family: -apple-system, BlinkMacSystemFont, 'SF Pro Text', 'Inter', sans-serif; background:#f5f5f7; min-height:100vh; display:flex; align-items:center; justify-content:center; padding:20px; -webkit-font-smoothing:antialiased; }
.card { background:#fff; border-radius:18px; padding:64px 48px; max-width:440px; width:100%; text-align:center; border:1px solid #e0e0e0; }
.icon { width:88px; height:88px; border-radius:50%; background:__COLOR__; color:#fff; font-size:44px; line-height:88px; margin:0 auto 32px; font-weight:600; }
h1 { font-size:34px; font-weight:600; color:#1d1d1f; margin-bottom:14px; letter-spacing:-0.374px; line-height:1.2; }
p { font-size:17px; color:#7a7a7a; line-height:1.47; margin-bottom:8px; }
.txn { font-size:13px; color:#aaa; margin-top:32px; font-family:'SF Mono', monospace; }
.btn { display:inline-block; margin-top:32px; padding:14px 32px; background:__COLOR__; color:#fff; border-radius:9999px; text-decoration:none; font-size:17px; font-weight:400; transition:transform .15s; }
.btn:active { transform:scale(0.95); }
</style></head><body><div class=""card""><div class=""icon"">__ICON__</div><h1>__TITLE__</h1><p>__MSG__</p><p class=""txn"">__TXN__</p><a href=""javascript:window.close()"" class=""btn"">Đóng</a></div></body></html>";

        return template
            .Replace("__COLOR__", color).Replace("__ICON__", icon)
            .Replace("__TITLE__", title).Replace("__MSG__", msg)
            .Replace("__TXN__", txnCode);
    }
}

public record CreatePaymentRequest(int PoiId, string DeviceId, string? SessionId);

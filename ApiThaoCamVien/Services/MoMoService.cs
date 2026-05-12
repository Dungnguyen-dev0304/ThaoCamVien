using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApiThaoCamVien.Services;

/// <summary>
/// Tích hợp MoMo Wallet (sandbox) — Pay With App / Pay With QR Code v2.
///
/// Flow:
///   1. Server gọi POST /v2/gateway/api/create với HMAC-SHA256 signature.
///   2. MoMo trả về payUrl (web) + deeplink (mobile) + qrCodeUrl.
///   3. App hiển thị QR (encode payUrl) hoặc mở deeplink.
///   4. User trả tiền trong app MoMo.
///   5. MoMo gửi IPN POST tới /api/payment/momo-ipn → server verify signature.
///
/// Sandbox docs: https://developers.momo.vn/v3/docs/payment/api/wallet/onetime
/// Test credentials (public sandbox):
///   PartnerCode  = MOMO
///   AccessKey    = F8BBA842ECF85
///   SecretKey    = K951B6PE1waDMi640xX08PD3vg6EkVlz
/// </summary>
public class MoMoService
{
    private readonly string _partnerCode;
    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly string _endpoint;
    private readonly string _redirectUrl;
    private readonly string _ipnUrl;
    private readonly int _timeoutMinutes;
    private readonly ILogger<MoMoService> _logger;
    private readonly HttpClient _http;

    public MoMoService(IConfiguration cfg, ILogger<MoMoService> logger, IHttpClientFactory httpFactory)
    {
        _logger = logger;
        var s = cfg.GetSection("MoMo");
        _partnerCode = (s["PartnerCode"] ?? "").Trim();
        _accessKey   = (s["AccessKey"] ?? "").Trim();
        _secretKey   = (s["SecretKey"] ?? "").Trim();
        _endpoint    = (s["Endpoint"] ?? "https://test-payment.momo.vn/v2/gateway/api/create").Trim();
        _redirectUrl = (s["RedirectUrl"] ?? "http://localhost:5281/api/payment/momo-return").Trim();
        _ipnUrl      = (s["IpnUrl"] ?? "http://localhost:5281/api/payment/momo-ipn").Trim();
        _timeoutMinutes = int.TryParse(s["TimeoutMinutes"], out var t) ? t : 15;

        if (string.IsNullOrEmpty(_partnerCode) || string.IsNullOrEmpty(_accessKey) || string.IsNullOrEmpty(_secretKey))
            throw new InvalidOperationException("MoMo: PartnerCode/AccessKey/SecretKey not configured");

        _http = httpFactory.CreateClient(nameof(MoMoService));
        _http.Timeout = TimeSpan.FromSeconds(20);

        _logger.LogInformation("MoMoService init. Partner={Partner} AccessKey={Ak} Endpoint={Ep}",
            _partnerCode, _accessKey, _endpoint);
    }

    /// <summary>
    /// Tạo giao dịch trên MoMo. Trả về (payUrl, deeplink, qrCodeUrl, momoOrderId).
    /// payUrl = URL có thể encode thành QR cho user quét.
    /// </summary>
    public async Task<MoMoCreateResult> CreateOrderAsync(
        string transactionCode,
        long amountVnd,
        string orderInfo,
        CancellationToken ct = default)
    {
        var requestId = transactionCode;            // dùng cùng id cho dễ trace
        var orderId = transactionCode;
        var extraData = "";
        var requestType = "captureWallet";          // QR + deeplink wallet

        // Raw signature data (sort theo alphabet — MoMo yêu cầu thứ tự CỐ ĐỊNH này)
        var rawSignature =
            $"accessKey={_accessKey}" +
            $"&amount={amountVnd}" +
            $"&extraData={extraData}" +
            $"&ipnUrl={_ipnUrl}" +
            $"&orderId={orderId}" +
            $"&orderInfo={orderInfo}" +
            $"&partnerCode={_partnerCode}" +
            $"&redirectUrl={_redirectUrl}" +
            $"&requestId={requestId}" +
            $"&requestType={requestType}";

        var signature = HmacSha256(_secretKey, rawSignature);

        var body = new
        {
            partnerCode = _partnerCode,
            partnerName = "ThaoCamVien",
            storeId     = "ThaoCamVienStore",
            requestId,
            amount      = amountVnd,
            orderId,
            orderInfo,
            redirectUrl = _redirectUrl,
            ipnUrl      = _ipnUrl,
            lang        = "vi",
            extraData,
            requestType,
            signature
        };

        _logger.LogInformation("MoMo CreateOrder | rawSig={Raw}", rawSignature);

        try
        {
            var resp = await _http.PostAsJsonAsync(_endpoint, body, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("MoMo response: {Status} {Body}", resp.StatusCode, json);

            if (!resp.IsSuccessStatusCode)
                return new MoMoCreateResult(false, null, null, null, $"HTTP {resp.StatusCode}: {json}");

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var resultCode = root.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1;
            var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : "";

            if (resultCode != 0)
                return new MoMoCreateResult(false, null, null, null, $"Code={resultCode}: {message}");

            var payUrl = root.TryGetProperty("payUrl", out var pu) ? pu.GetString() : null;
            var deeplink = root.TryGetProperty("deeplink", out var dl) ? dl.GetString() : null;
            var qrCodeUrl = root.TryGetProperty("qrCodeUrl", out var qr) ? qr.GetString() : null;

            return new MoMoCreateResult(true, payUrl, deeplink, qrCodeUrl, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MoMo CreateOrder error");
            return new MoMoCreateResult(false, null, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Verify MoMo IPN callback. MoMo gửi POST application/json.
    /// </summary>
    public bool VerifyIpnSignature(JsonElement body, out int resultCode, out string orderId)
    {
        resultCode = body.TryGetProperty("resultCode", out var rc) ? rc.GetInt32() : -1;
        orderId = body.TryGetProperty("orderId", out var o) ? o.GetString() ?? "" : "";

        var receivedSig = body.TryGetProperty("signature", out var s) ? s.GetString() ?? "" : "";

        // Build raw chuẩn theo MoMo IPN spec
        string Get(string k) => body.TryGetProperty(k, out var v) ? (v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString() ?? "") : "";

        var raw =
            $"accessKey={_accessKey}" +
            $"&amount={Get("amount")}" +
            $"&extraData={Get("extraData")}" +
            $"&message={Get("message")}" +
            $"&orderId={Get("orderId")}" +
            $"&orderInfo={Get("orderInfo")}" +
            $"&orderType={Get("orderType")}" +
            $"&partnerCode={Get("partnerCode")}" +
            $"&payType={Get("payType")}" +
            $"&requestId={Get("requestId")}" +
            $"&responseTime={Get("responseTime")}" +
            $"&resultCode={Get("resultCode")}" +
            $"&transId={Get("transId")}";

        var expected = HmacSha256(_secretKey, raw);
        var match = string.Equals(expected, receivedSig, StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("MoMo IPN verify | raw={Raw} | expected={E} | received={R} | match={M}",
            raw, expected, receivedSig, match);

        return match;
    }

    public DateTime GetQrExpiry() => DateTime.UtcNow.AddMinutes(_timeoutMinutes);

    public (string Partner, string AccessKey, int SecretLen, string Endpoint) DebugInfo()
        => (_partnerCode, _accessKey, _secretKey.Length, _endpoint);

    // ── Helpers ────────────────────────────────────────────────────────────
    private static string HmacSha256(string key, string data)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = h.ComputeHash(Encoding.UTF8.GetBytes(data));
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

public record MoMoCreateResult(
    bool Success,
    string? PayUrl,
    string? Deeplink,
    string? QrCodeUrl,
    string? Message);

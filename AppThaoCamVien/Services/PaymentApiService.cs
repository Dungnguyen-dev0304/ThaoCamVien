using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

/// <summary>
/// Wrapper gọi các endpoint /api/payment/* của API.
/// Dùng chung HttpClient với ApiService nhưng có namespace riêng cho rõ ràng.
/// </summary>
public sealed class PaymentApiService
{
    private readonly ApiService _api;
    private readonly HttpClient _http;

    private const string DeviceIdKey = "TCV_DeviceId";

    public PaymentApiService(ApiService api)
    {
        _api = api;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
#if DEBUG
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
#endif
    }

    /// <summary>Lấy hoặc tạo Device ID ổn định (lưu trong Preferences).</summary>
    public string GetOrCreateDeviceId()
    {
        var id = Preferences.Default.Get(DeviceIdKey, string.Empty);
        if (string.IsNullOrEmpty(id))
        {
            // Format: ANDROID-{model}-{guid8}
            var prefix = DeviceInfo.Platform.ToString().ToUpper();
            var model = DeviceInfo.Model?.Replace(" ", "") ?? "UNK";
            var rand = Guid.NewGuid().ToString("N")[..8];
            id = $"{prefix}-{model}-{rand}";
            Preferences.Default.Set(DeviceIdKey, id);
        }
        return id;
    }

    private string Url(string path) => $"{_api.BaseUrl.TrimEnd('/')}{path}";

    /// <summary>GET /api/payment/check/{poiId} — POI có premium không + giá.</summary>
    public async Task<PremiumCheckResult?> CheckPremiumAsync(int poiId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync(Url($"/api/payment/check/{poiId}"), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PremiumCheckResult>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentApi] CheckPremium error: {ex.Message}");
            return null;
        }
    }

    /// <summary>GET /api/payment/access/{poiId}?deviceId=xxx — Device đã mua chưa.</summary>
    public async Task<bool> HasAccessAsync(int poiId, CancellationToken ct = default)
    {
        try
        {
            var deviceId = GetOrCreateDeviceId();
            var resp = await _http.GetAsync(
                Url($"/api/payment/access/{poiId}?deviceId={Uri.EscapeDataString(deviceId)}"), ct);
            if (!resp.IsSuccessStatusCode) return false;
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            return doc.TryGetProperty("hasAccess", out var prop) && prop.GetBoolean();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentApi] HasAccess error: {ex.Message}");
            return false;
        }
    }

    /// <summary>POST /api/payment/create — tạo giao dịch + lấy URL VNPay.</summary>
    public async Task<CreatePaymentResult?> CreatePaymentAsync(int poiId, CancellationToken ct = default)
    {
        try
        {
            var body = new
            {
                poiId,
                deviceId = GetOrCreateDeviceId(),
                sessionId = Preferences.Default.Get("ClientSessionId", string.Empty)
            };

            var resp = await _http.PostAsJsonAsync(Url("/api/payment/create"), body, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"[PaymentApi] Create failed: {resp.StatusCode} - {err}");
                return null;
            }
            return await resp.Content.ReadFromJsonAsync<CreatePaymentResult>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentApi] CreatePayment error: {ex.Message}");
            return null;
        }
    }

    /// <summary>GET /api/payment/status/{txnCode} — polling trạng thái.</summary>
    public async Task<PaymentStatusResult?> GetStatusAsync(string txnCode, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync(Url($"/api/payment/status/{Uri.EscapeDataString(txnCode)}"), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PaymentStatusResult>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentApi] GetStatus error: {ex.Message}");
            return null;
        }
    }
}

// ── DTOs (matches API JSON shape) ─────────────────────────────────────────────

public class PremiumCheckResult
{
    public int poiId { get; set; }
    public bool isPremium { get; set; }
    public decimal price { get; set; }
    public string? currency { get; set; }
    public string? name { get; set; }
}

public class CreatePaymentResult
{
    public string? transactionCode { get; set; }
    public string? vnpayUrl { get; set; }       // legacy field — = payUrl, encode QR
    public string? payUrl { get; set; }         // MoMo payUrl (web/QR)
    public string? deeplink { get; set; }       // MoMo deeplink (mobile app)
    public string? qrCodeUrl { get; set; }      // MoMo official QR image URL
    public DateTime? qrExpiredAt { get; set; }
    public decimal amount { get; set; }
    public string? currency { get; set; }
    public string? status { get; set; }
    public string? poiName { get; set; }
    public string? paymentMethod { get; set; }
    public bool alreadyOwned { get; set; }
    public string? message { get; set; }
}

public class PaymentStatusResult
{
    public string? transactionCode { get; set; }
    public string? status { get; set; }
    public int poiId { get; set; }
    public decimal amount { get; set; }
    public DateTime? completedAt { get; set; }
    public string? failureReason { get; set; }
    public DateTime? qrExpiredAt { get; set; }
    public bool accessGranted { get; set; }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.ViewModels;

/// <summary>
/// PaymentViewModel — quản lý vòng đời 1 lần thanh toán VNPay.
///
/// Flow:
///   1. SetContext(poi) → biết POI cần mua
///   2. CreateAsync()   → tạo giao dịch + lấy URL VNPay → QR hiển thị
///   3. StartPolling()  → poll trạng thái mỗi 3 giây
///   4. Khi status == "success" → bắn event PaymentSucceeded → page đóng
/// </summary>
public class PaymentViewModel : INotifyPropertyChanged
{
    private readonly PaymentApiService _api;
    private CancellationTokenSource? _pollCts;

    public PaymentViewModel(PaymentApiService api)
    {
        _api = api;
    }

    // ── State exposed to View ─────────────────────────────────────────────

    private Poi? _poi;
    public Poi? CurrentPoi
    {
        get => _poi;
        set { _poi = value; OnPropertyChanged(); }
    }

    private string _poiName = "";
    public string PoiName
    {
        get => _poiName;
        set { _poiName = value; OnPropertyChanged(); }
    }

    private decimal _amount;
    public decimal Amount
    {
        get => _amount;
        set { _amount = value; OnPropertyChanged(); OnPropertyChanged(nameof(AmountDisplay)); }
    }

    public string AmountDisplay => $"{Amount:#,##0} ₫";

    private string? _qrCodeValue;
    /// <summary>Chuỗi MoMo qrCodeUrl được encode thành QR code trong View.</summary>
    public string? QrCodeValue
    {
        get => _qrCodeValue;
        set { _qrCodeValue = value; OnPropertyChanged(); }
    }

    private string? _deeplink;
    /// <summary>MoMo deeplink (momo://...) — dùng khi user bấm "Mở ứng dụng MoMo".</summary>
    public string? Deeplink
    {
        get => _deeplink;
        set { _deeplink = value; OnPropertyChanged(); }
    }

    private string? _payUrl;
    /// <summary>MoMo payUrl (https://...) — fallback nếu deeplink không mở được.</summary>
    public string? PayUrl
    {
        get => _payUrl;
        set { _payUrl = value; OnPropertyChanged(); }
    }

    private string? _transactionCode;
    public string? TransactionCode
    {
        get => _transactionCode;
        set { _transactionCode = value; OnPropertyChanged(); }
    }

    private string _status = "idle"; // idle | loading | pending | processing | success | failed | expired | error
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
    }

    public string StatusDisplay => Status switch
    {
        "loading"    => "Đang tạo giao dịch...",
        "pending"    => "Quét QR để thanh toán",
        "processing" => "Đang xử lý thanh toán...",
        "success"    => "Thanh toán thành công",
        "failed"     => "Thanh toán thất bại",
        "expired"    => "Mã QR đã hết hạn",
        "error"      => "Lỗi kết nối",
        _ => ""
    };

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    private TimeSpan _timeRemaining;
    public TimeSpan TimeRemaining
    {
        get => _timeRemaining;
        set { _timeRemaining = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeRemainingDisplay)); }
    }

    public string TimeRemainingDisplay =>
        TimeRemaining.TotalSeconds <= 0 ? "00:00" : $"{(int)TimeRemaining.TotalMinutes:D2}:{TimeRemaining.Seconds:D2}";

    private DateTime? _qrExpiresAt;

    // ── Events ─────────────────────────────────────────────────────────────
    public event EventHandler? PaymentSucceeded;
    public event EventHandler<string>? PaymentFailed;
    public event EventHandler? AlreadyOwned;

    // ── Public API ─────────────────────────────────────────────────────────

    public void SetContext(Poi poi)
    {
        CurrentPoi = poi;
        PoiName = poi.Name ?? "Premium Content";
        Amount = poi.PremiumPrice ?? 50000;
        Status = "idle";
        ErrorMessage = null;
    }

    /// <summary>Tạo giao dịch + lấy URL VNPay làm QR. Trả về true nếu OK.</summary>
    public async Task<bool> CreateAsync(CancellationToken ct = default)
    {
        if (CurrentPoi == null) return false;

        Status = "loading";
        ErrorMessage = null;

        var result = await _api.CreatePaymentAsync(CurrentPoi.PoiId, ct);

        if (result == null)
        {
            Status = "error";
            ErrorMessage = "Không thể kết nối tới máy chủ. Kiểm tra mạng và thử lại.";
            return false;
        }

        if (result.alreadyOwned)
        {
            Status = "success";
            AlreadyOwned?.Invoke(this, EventArgs.Empty);
            return true;
        }

        // Ưu tiên qrCodeUrl (chuỗi QR native MoMo) → app MoMo nhận diện là QR thanh toán
        // → hiện thẳng ví + nút "Thanh toán". Nếu không có thì fallback payUrl (web gateway).
        var qrValue = !string.IsNullOrEmpty(result.qrCodeUrl) ? result.qrCodeUrl
                    : !string.IsNullOrEmpty(result.payUrl)    ? result.payUrl
                    : result.vnpayUrl;

        if (string.IsNullOrEmpty(qrValue) || string.IsNullOrEmpty(result.transactionCode))
        {
            Status = "error";
            ErrorMessage = result.message ?? "Phản hồi không hợp lệ từ máy chủ.";
            return false;
        }

        QrCodeValue = qrValue;
        Deeplink = result.deeplink;
        PayUrl = result.payUrl;
        TransactionCode = result.transactionCode;
        _qrExpiresAt = result.qrExpiredAt;
        Amount = result.amount;
        Status = "pending";

        StartPolling();
        StartCountdown();
        return true;
    }

    /// <summary>Bắt đầu poll status mỗi 3 giây cho đến khi success/failed/expired.</summary>
    public void StartPolling()
    {
        StopPolling();
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
    }

    public void StopPolling()
    {
        try
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
        }
        catch { }
        _pollCts = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !string.IsNullOrEmpty(TransactionCode))
        {
            try
            {
                await Task.Delay(3000, ct);
                if (ct.IsCancellationRequested) break;

                var status = await _api.GetStatusAsync(TransactionCode!, ct);
                if (status == null) continue;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Status = status.status ?? "pending";

                    if (status.status == "success")
                    {
                        StopPolling();
                        PaymentSucceeded?.Invoke(this, EventArgs.Empty);
                    }
                    else if (status.status == "failed")
                    {
                        StopPolling();
                        ErrorMessage = status.failureReason ?? "Giao dịch thất bại";
                        PaymentFailed?.Invoke(this, ErrorMessage);
                    }
                    else if (status.status == "expired")
                    {
                        StopPolling();
                        ErrorMessage = "Mã QR đã hết hạn. Vui lòng tạo mã mới.";
                    }
                });

                if (status.status is "success" or "failed" or "expired") break;
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentVM] Poll error: {ex.Message}");
            }
        }
    }

    private void StartCountdown()
    {
        if (_qrExpiresAt == null) return;
        Application.Current?.Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (_qrExpiresAt == null || Status is "success" or "failed" or "expired")
            {
                TimeRemaining = TimeSpan.Zero;
                return false;
            }

            var remain = _qrExpiresAt.Value - DateTime.UtcNow;
            TimeRemaining = remain > TimeSpan.Zero ? remain : TimeSpan.Zero;
            return TimeRemaining > TimeSpan.Zero;
        });
    }

    /// <summary>Tạo lại QR code (khi expired hoặc user nhấn Refresh).</summary>
    public Task<bool> RefreshAsync(CancellationToken ct = default) => CreateAsync(ct);

    public void Cleanup()
    {
        StopPolling();
        QrCodeValue = null;
        TransactionCode = null;
    }

    // ── INotifyPropertyChanged ───────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name ?? string.Empty));
}

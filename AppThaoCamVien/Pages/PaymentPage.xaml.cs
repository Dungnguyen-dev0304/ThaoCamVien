using AppThaoCamVien.Services;
using AppThaoCamVien.ViewModels;
using SharedThaoCamVien.Models;
using System.Net.Http;

namespace AppThaoCamVien.Pages;

/// <summary>
/// PaymentPage — màn hình thanh toán VNPay với QR code (Apple design).
///
/// Lifecycle:
///   1. Page mở → SetPoi(poi) đặt context
///   2. OnAppearing → CreateAsync() → tạo giao dịch + lấy QR
///   3. ViewModel poll mỗi 3s
///   4. Sự kiện PaymentSucceeded → hiển thị success overlay → close page
///   5. PaymentFailed → hiện error → cho user retry
/// </summary>
public partial class PaymentPage : ContentPage
{
    private readonly PaymentViewModel _vm;
    private Poi? _poi;
    private TaskCompletionSource<bool>? _resultTcs;

    /// <summary>
    /// Task hoàn thành khi page đóng. true = thanh toán thành công.
    /// Caller dùng: bool ok = await page.WaitForResultAsync();
    /// </summary>
    public Task<bool> WaitForResultAsync()
    {
        _resultTcs ??= new TaskCompletionSource<bool>();
        return _resultTcs.Task;
    }

    private readonly ApiService _api;

    public PaymentPage(PaymentViewModel vm, ApiService api)
    {
        InitializeComponent();
        _vm = vm;
        _api = api;
        BindingContext = _vm;

        _vm.PaymentSucceeded += OnPaymentSucceeded;
        _vm.PaymentFailed += OnPaymentFailed;
        _vm.AlreadyOwned += OnAlreadyOwned;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    /// <summary>Caller gọi trước khi PushAsync để truyền POI vào page.</summary>
    public void SetPoi(Poi poi)
    {
        _poi = poi;
        _vm.SetContext(poi);
    }

    // ─── Lifecycle ─────────────────────────────────────────────────────────

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_poi == null)
        {
            await DisplayAlert("Lỗi", "Không xác định được nội dung cần thanh toán.", "Đóng");
            await Navigation.PopAsync();
            return;
        }

        UpdateLoadingState(true);
        var ok = await _vm.CreateAsync();
        UpdateLoadingState(false);

        if (!ok)
        {
            ShowError(_vm.ErrorMessage ?? "Không thể tạo giao dịch.");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.StopPolling();
    }

    // ─── ViewModel events ──────────────────────────────────────────────────

    private void OnPaymentSucceeded(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            SuccessOverlay.IsVisible = true;
            // Cho user thấy success animation 1.5 giây rồi mới đóng
            await Task.Delay(1500);
            _resultTcs?.TrySetResult(true);
            try { await Navigation.PopAsync(); } catch { }
        });
    }

    private void OnPaymentFailed(object? sender, string reason)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ShowError(reason);
            // User có thể nhấn Refresh để tạo giao dịch mới
            RefreshBtn.IsVisible = true;
            OpenAppBtn.IsVisible = false;
        });
    }

    private void OnAlreadyOwned(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlert("Đã mua",
                "Bạn đã sở hữu nội dung này. Đang mở khóa...", "OK");
            _resultTcs?.TrySetResult(true);
            try { await Navigation.PopAsync(); } catch { }
        });
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Cập nhật UI dựa trên Status thay đổi
        if (e.PropertyName != nameof(_vm.Status)) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (_vm.Status)
            {
                case "loading":
                    UpdateLoadingState(true);
                    ExpiredOverlay.IsVisible = false;
                    StatusDot.BackgroundColor = Color.FromArgb("#0066cc");
                    break;
                case "pending":
                    UpdateLoadingState(false);
                    ExpiredOverlay.IsVisible = false;
                    StatusDot.BackgroundColor = Color.FromArgb("#0066cc");
                    RefreshBtn.IsVisible = false;
                    OpenAppBtn.IsVisible = true;
                    break;
                case "processing":
                    StatusDot.BackgroundColor = Color.FromArgb("#0066cc");
                    break;
                case "success":
                    StatusDot.BackgroundColor = Color.FromArgb("#28a745");
                    break;
                case "failed":
                    StatusDot.BackgroundColor = Color.FromArgb("#d32f2f");
                    UpdateLoadingState(false);
                    break;
                case "expired":
                    UpdateLoadingState(false);
                    ExpiredOverlay.IsVisible = true;
                    ExpiredText.Text = "Mã QR đã hết hạn";
                    StatusDot.BackgroundColor = Color.FromArgb("#d32f2f");
                    RefreshBtn.IsVisible = true;
                    OpenAppBtn.IsVisible = false;
                    break;
                case "error":
                    UpdateLoadingState(false);
                    ShowError(_vm.ErrorMessage ?? "Có lỗi xảy ra.");
                    RefreshBtn.IsVisible = true;
                    OpenAppBtn.IsVisible = false;
                    break;
            }
        });
    }

    // ─── UI Helpers ────────────────────────────────────────────────────────

    private void UpdateLoadingState(bool loading)
    {
        LoadingOverlay.IsVisible = loading;
        QrCodeView.IsVisible = !loading && !string.IsNullOrEmpty(_vm.QrCodeValue);
    }

    private void ShowError(string msg)
    {
        ErrorPanel.IsVisible = true;
        ErrorLabel.Text = msg;
    }

    private void HideError()
    {
        ErrorPanel.IsVisible = false;
    }

    // ─── Button Handlers ───────────────────────────────────────────────────

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        HideError();
        ExpiredOverlay.IsVisible = false;
        UpdateLoadingState(true);
        await _vm.RefreshAsync();
    }

    private async void OnOpenVnPayClicked(object sender, EventArgs e)
    {
        // Ưu tiên deeplink (momo://...) → mở thẳng app MoMo với màn hình xác nhận thanh toán.
        // Nếu thiết bị không cài MoMo hoặc deeplink không mở được, fallback sang payUrl (web gateway).
        var deeplink = _vm.Deeplink;
        var fallback = _vm.PayUrl ?? _vm.QrCodeValue;

        if (string.IsNullOrEmpty(deeplink) && string.IsNullOrEmpty(fallback))
        {
            await DisplayAlert("Lỗi", "Chưa có thông tin thanh toán.", "OK");
            return;
        }

        if (!string.IsNullOrEmpty(deeplink))
        {
            try
            {
                var opened = await Launcher.Default.TryOpenAsync(new Uri(deeplink));
                if (opened) return;
            }
            catch { /* rơi xuống fallback */ }
        }

        if (!string.IsNullOrEmpty(fallback))
        {
            try
            {
                await Launcher.Default.OpenAsync(new Uri(fallback));
                return;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Không mở được",
                    $"Hãy quét QR bằng app MoMo.\n\n({ex.Message})", "OK");
                return;
            }
        }

        await DisplayAlert("Chưa cài MoMo",
            "Không mở được app MoMo trên thiết bị này. Hãy quét QR phía trên bằng máy khác có cài MoMo.",
            "OK");
    }

    /// <summary>
    /// DEMO: Gọi /api/payment/dev-mark-success/{txnCode} để force IPN thành công.
    /// Sandbox MoMo public không có ví test thật nên cần endpoint này cho demo.
    /// </summary>
    private async void OnDevSuccessClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_vm.TransactionCode))
        {
            await DisplayAlert("Lỗi", "Chưa có mã giao dịch.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Demo · Giả lập thanh toán?",
            $"Đánh dấu giao dịch {_vm.TransactionCode} là THÀNH CÔNG để demo.\n\nTrong môi trường thật bước này được thực hiện bởi MoMo qua webhook.",
            "Đồng ý",
            "Huỷ");
        if (!confirm) return;

        try
        {
            DevSuccessBtn.IsEnabled = false;
            DevSuccessBtn.Text = "Đang xử lý...";

            var handler = new HttpClientHandler();
#if DEBUG
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"{_api.BaseUrl.TrimEnd('/')}/api/payment/dev-mark-success/{_vm.TransactionCode}";
            var resp = await http.GetAsync(url);

            if (resp.IsSuccessStatusCode)
            {
                // ViewModel polling sẽ tự nhận status=success trong 3 giây tới
                // → tự bật Success Overlay + đóng page
                await DisplayAlert("Thành công", "Đã đánh dấu thanh toán thành công.\nÂm thanh sẽ tự phát trong giây lát.", "OK");
            }
            else
            {
                var err = await resp.Content.ReadAsStringAsync();
                await DisplayAlert("Lỗi", $"API trả về {resp.StatusCode}\n{err}", "OK");
                DevSuccessBtn.IsEnabled = true;
                DevSuccessBtn.Text = "✓  Tôi đã thanh toán (Demo)";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi mạng", ex.Message, "OK");
            DevSuccessBtn.IsEnabled = true;
            DevSuccessBtn.Text = "✓  Tôi đã thanh toán (Demo)";
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Hủy thanh toán?",
            "Bạn có chắc muốn hủy giao dịch này?", "Hủy giao dịch", "Tiếp tục thanh toán");
        if (!confirm) return;

        _vm.StopPolling();
        _resultTcs?.TrySetResult(false);
        try { await Navigation.PopAsync(); } catch { }
    }

    ~PaymentPage()
    {
        try
        {
            _vm.PaymentSucceeded -= OnPaymentSucceeded;
            _vm.PaymentFailed -= OnPaymentFailed;
            _vm.AlreadyOwned -= OnAlreadyOwned;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }
        catch { }
    }
}

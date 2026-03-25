using ZXing.Net.Maui;
using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class QrPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private bool _isProcessing = false;
    private bool _isFlashOn = false;
    private CancellationTokenSource? _scanAnimCts;

    public QrPage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BarcodeReader.IsDetecting = true;
        _isProcessing = false;
        StatusLabel.Text = "Đang tìm kiếm mã QR...";
        StartScanAnimation();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;
        _scanAnimCts?.Cancel();
        if (_isFlashOn)
        {
            _isFlashOn = false;
            BarcodeReader.IsTorchOn = false;
        }
    }

    private void StartScanAnimation()
    {
        _scanAnimCts = new CancellationTokenSource();
        var token = _scanAnimCts.Token;

        _ = Task.Run(async () =>
        {
            bool goingDown = true;
            while (!token.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    double targetY = goingDown ? 220 : 10;
                    await ScanLine.TranslateTo(0, targetY, 1200, Easing.SinInOut);
                    goingDown = !goingDown;
                });
            }
        }, token);
    }

    /// <summary>
    /// ZXing callback — QrCodeData khớp với QrCode.QrCodeData trong DB
    /// </summary>
    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing) return;
        var result = e.Results?.FirstOrDefault();
        if (result == null) return;

        _isProcessing = true;
        var qrData = result.Value;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleScannedCodeAsync(qrData);
        });
    }

    private async Task HandleScannedCodeAsync(string qrData)
    {
        StatusLabel.Text = $"Đã nhận mã, đang tra cứu...";

        // Tra cứu POI theo QrCodeData trong bảng QrCode
        var poi = await _databaseService.GetPoiByQrDataAsync(qrData);

        if (poi != null)
        {
            await NavigateToAudioAsync(poi);
        }
        else
        {
            await DisplayAlert("Không nhận ra",
                $"Mã QR không hợp lệ hoặc chưa được đăng ký.\nVui lòng thử lại.", "OK");
            _isProcessing = false;
            StatusLabel.Text = "Đang tìm kiếm mã QR...";
        }
    }

    private async Task NavigateToAudioAsync(Poi poi)
    {
        var audioPage = IPlatformApplication.Current.Services.GetService<StoryAudioPage>();
        if (audioPage != null)
        {
            audioPage.LoadPoi(poi);
            await Navigation.PushAsync(audioPage);
        }
        _isProcessing = false;
        StatusLabel.Text = "Đang tìm kiếm mã QR...";
    }

    private void OnToggleFlash(object sender, TappedEventArgs e)
    {
        _isFlashOn = !_isFlashOn;
        BarcodeReader.IsTorchOn = _isFlashOn;
        FlashButton.BackgroundColor = _isFlashOn
            ? Color.FromArgb("#4CAF50")
            : Color.FromArgb("#2C2C2E");
    }

    private async void OnOpenNumpad(object sender, TappedEventArgs e)
    {
        var numpadPage = IPlatformApplication.Current.Services.GetService<NumpadPage>();
        if (numpadPage != null)
            await Navigation.PushAsync(numpadPage);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}

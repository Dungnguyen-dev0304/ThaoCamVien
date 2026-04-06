using ZXing.Net.Maui;
using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class QrPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly IServiceProvider _sp;
    private bool _busy = false;
    private bool _flashOn = false;
    private CancellationTokenSource? _animCts;

    public QrPage(DatabaseService db, IServiceProvider sp)
    {
        InitializeComponent();
        _db = db;
        _sp = sp;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BarcodeReader.IsDetecting = true;
        _busy = false;
        StatusLabel.Text = GetString("TxtQrSearching", "Đang tìm kiếm mã QR...");
        StartAnim();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;
        _animCts?.Cancel();
        if (_flashOn) { _flashOn = false; BarcodeReader.IsTorchOn = false; }
    }

    private void StartAnim()
    {
        _animCts = new CancellationTokenSource();
        var tok = _animCts.Token;
        _ = Task.Run(async () =>
        {
            bool down = true;
            while (!tok.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await ScanLine.TranslateTo(0, down ? 220 : 10, 1200, Easing.SinInOut);
                    down = !down;
                });
            }
        }, tok);
    }

    private void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_busy) return;
        var result = e.Results?.FirstOrDefault();
        if (result == null) return;
        _busy = true;
        MainThread.BeginInvokeOnMainThread(async () => await HandleAsync(result.Value));
    }

    private async Task HandleAsync(string qrData)
    {
        StatusLabel.Text = "Đang tải thông tin...";

        try
        {
            var poi = await _db.GetPoiByQrDataAsync(qrData);
            if (poi != null)
            {
                // Phát narration ngay lập tức
                var narration = _sp.GetService<NarrationEngine>();
                _ = narration?.PlayAsync(poi, forcePlay: true);

                var page = _sp.GetService<StoryAudioPage>();
                if (page != null)
                {
                    page.LoadPoi(poi);
                    await Navigation.PushAsync(page);
                }
            }
            else
            {
                await DisplayAlert("Không nhận ra",
                    "Mã QR này chưa được đăng ký trong hệ thống.\nVui lòng thử lại.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
        finally
        {
            _busy = false;
            StatusLabel.Text = GetString("TxtQrSearching", "Đang tìm kiếm mã QR...");
        }
    }

    private void OnToggleFlash(object sender, TappedEventArgs e)
    {
        _flashOn = !_flashOn;
        BarcodeReader.IsTorchOn = _flashOn;
        FlashButton.BackgroundColor = _flashOn
            ? Color.FromArgb("#4CAF50") : Color.FromArgb("#2C2C2E");
    }

    private async void OnOpenNumpad(object sender, TappedEventArgs e)
    {
        var page = _sp.GetService<NumpadPage>();
        if (page != null) await Navigation.PushAsync(page);
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private static string GetString(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is string s)
            return s;
        return fallback;
    }
}

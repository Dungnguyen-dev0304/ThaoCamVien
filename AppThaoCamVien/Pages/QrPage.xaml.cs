using ZXing.Net.Maui;
using AppThaoCamVien.Services;
using AppThaoCamVien.ViewModels;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class QrPage : ContentPage
{
    private readonly IServiceProvider _sp;
    private readonly QrPageViewModel _vm;
    private bool _busy = false;
    private bool _flashOn = false;
    private CancellationTokenSource? _animCts;

    public QrPage(QrPageViewModel vm, IServiceProvider sp)
    {
        InitializeComponent();
        _sp = sp;
        _vm = vm;
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BarcodeReader.IsDetecting = true;
        _busy = false;
        _ = _vm.SafeReloadAsync();
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
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await ScanLine.TranslateToAsync(0, down ? 220 : 10, 1200, Easing.SinInOut);
                        down = !down;
                    });
                }
                catch
                {
                    break;
                }
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
        try
        {
            await _vm.ResolveQrAsync(qrData);

            var poi = _vm.ResolvedPoiModel;
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
        }
        catch (Exception ex)
        {
            // VM sẽ tự set state Error nếu ResolveQrAsync bắt lỗi.
            System.Diagnostics.Debug.WriteLine($"[QrPage] HandleAsync error: {ex.Message}");
        }
        finally
        {
            _busy = false;
            // Giữ UI scanning ổn định; state container sẽ phản ánh Empty/Error.
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

    
}

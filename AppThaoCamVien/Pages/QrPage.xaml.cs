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
    private bool _cameraPermissionGranted = false;
    private CancellationTokenSource? _animCts;

    public QrPage(QrPageViewModel vm, IServiceProvider sp)
    {
        InitializeComponent();
        _sp = sp;
        _vm = vm;
        BindingContext = _vm;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _busy = false;

        await EnsureCameraPermissionAsync();

        if (_cameraPermissionGranted)
        {
            await RestartCameraPreviewAsync();
        }

        _ = _vm.SafeReloadAsync();
        StartAnim();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Tắt detecting trước khi tắt flash
        try { BarcodeReader.IsDetecting = false; } catch { }

        if (_flashOn)
        {
            _flashOn = false;
            try { BarcodeReader.IsTorchOn = false; } catch { }
        }
        _animCts?.Cancel();
    }

    private async Task RestartCameraPreviewAsync()
    {
        // Một số máy thật mở camera thành công nhưng preview đen.
        // Sequence này ép reattach view + restart detector.
        try
        {
            BarcodeReader.IsTorchOn = false;
            BarcodeReader.IsDetecting = false;
            await Task.Delay(120);

            BarcodeReader.IsVisible = false;
            await Task.Delay(80);
            BarcodeReader.IsVisible = true;
            await Task.Delay(180);

            BarcodeReader.IsDetecting = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrPage] RestartCameraPreviewAsync error: {ex.Message}");
        }
    }
    private async Task EnsureCameraPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            _cameraPermissionGranted = (status == PermissionStatus.Granted);

            if (!_cameraPermissionGranted)
            {
                BarcodeReader.IsDetecting = false;


                if (status == PermissionStatus.Denied)
                {
                    var goSettings = await DisplayAlert(
                        "Quyền Camera",
                        "Ứng dụng cần quyền Camera để quét mã QR.\n" +
                        "Vui lòng vào Cài đặt → Quyền ứng dụng → Camera để cấp quyền.",
                        "Mở Cài đặt",
                        "Bỏ qua");

                    if (goSettings)
                    {
                        try { AppInfo.ShowSettingsUI(); }
                        catch { /* Một số device không support */ }
                    }
                }
                else
                {
                    await DisplayAlert(
                        "Quyền Camera",
                        "Ứng dụng cần quyền Camera để quét mã QR. Vui lòng cấp quyền.",
                        "OK");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrPage] Permission check error: {ex.Message}");
            _cameraPermissionGranted = false;
        }
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
                        await ScanLine.TranslateTo(0, down ? 220 : 10, 1200, Easing.SinInOut);
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
            System.Diagnostics.Debug.WriteLine($"[QrPage] HandleAsync error: {ex.Message}");
        }
        finally
        {
            _busy = false;
        }
    }

   
    private void OnToggleFlash(object sender, TappedEventArgs e)
    {
        if (!_cameraPermissionGranted) return;
        _flashOn = !_flashOn;
        try
        {
            BarcodeReader.IsTorchOn = _flashOn;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QrPage] Flash toggle error: {ex.Message}");
            _flashOn = false;
        }
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

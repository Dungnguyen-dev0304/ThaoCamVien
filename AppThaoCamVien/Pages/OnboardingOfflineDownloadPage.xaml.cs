using AppThaoCamVien.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Networking;

namespace AppThaoCamVien.Pages;

public partial class OnboardingOfflineDownloadPage : ContentPage
{
    private readonly IServiceProvider _sp;
    private readonly OfflineBundleDownloadService _download;
    private readonly string[] _langCodes = ["vi", "en", "ja"];
    private readonly string[] _langLabels = ["Tiếng Việt", "English", "日本語"];
    private bool _pickerReady;
    private bool _wasPaused;

    public OnboardingOfflineDownloadPage(IServiceProvider sp, OfflineBundleDownloadService download)
    {
        _sp = sp;
        _download = download;
        InitializeComponent();
        LangPicker.ItemsSource = _langLabels;
        var idx = Array.IndexOf(_langCodes, LanguageManager.Current);
        LangPicker.SelectedIndex = idx >= 0 ? idx : 0;
        _pickerReady = true;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        NavigationPage.SetHasNavigationBar(this, true);
        _download.ProgressChanged += OnDownloadProgress;
        _download.StatusChanged += OnDownloadStatus;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        RefreshUiFromProgress(_download.Progress);
        if (_download.IsCompleted)
        {
            DownloadBtn.IsVisible = false;
            ResumeBtn.IsVisible = false;
            EnterAppBtn.IsVisible = true;
            StatusLabel.Text = Application.Current?.Resources["OnboardingDownloadComplete"] as string ?? "";
        }
        else if (_download.Progress is > 0 and < 1)
        {
            _wasPaused = true;
            ResumeBtn.IsVisible = true;
            StatusLabel.Text = FormatNetworkLost(_download.Progress);
        }
    }

    protected override void OnDisappearing()
    {
        _download.ProgressChanged -= OnDownloadProgress;
        _download.StatusChanged -= OnDownloadStatus;
        Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        base.OnDisappearing();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.Internet)
            return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_wasPaused && ResumeBtn.IsVisible)
                StatusLabel.Text = Application.Current?.Resources["OnboardingProgressHint"] as string ?? "";
        });
    }

    private void OnDownloadProgress(object? sender, double p)
    {
        DownloadProgress.Progress = p;
        PercentLabel.Text = string.Format("{0:0}%", p * 100);
        RefreshUiFromProgress(p);
    }

    private void OnDownloadStatus(object? sender, string status)
    {
        if (status == "network_lost")
        {
            _wasPaused = true;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ResumeBtn.IsVisible = true;
                DownloadBtn.IsEnabled = true;
                StatusLabel.Text = FormatNetworkLost(_download.Progress);
            });
        }
        else if (status == "completed")
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _wasPaused = false;
                ResumeBtn.IsVisible = false;
                DownloadBtn.IsVisible = false;
                EnterAppBtn.IsVisible = true;
                StatusLabel.Text = Application.Current?.Resources["OnboardingDownloadComplete"] as string ?? "";
            });
        }
    }

    private void RefreshUiFromProgress(double p)
    {
        DownloadProgress.Progress = p;
        PercentLabel.Text = string.Format("{0:0}%", p * 100);
        if (p >= 1 - 0.0001)
        {
            DownloadBtn.IsVisible = false;
            ResumeBtn.IsVisible = false;
            EnterAppBtn.IsVisible = true;
        }
    }

    private static string FormatNetworkLost(double p)
    {
        var fmt = Application.Current?.Resources["OnboardingNetworkLost"] as string ?? "{0}";
        var pct = string.Format("{0:0}%", p * 100);
        return string.Format(fmt, pct);
    }

    private void OnLangPickerChanged(object? sender, EventArgs e)
    {
        if (!_pickerReady || LangPicker.SelectedIndex < 0)
            return;
        var code = _langCodes[LangPicker.SelectedIndex];
        LanguageManager.Apply(code);
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        DownloadBtn.IsEnabled = false;
        ResumeBtn.IsVisible = false;
        StatusLabel.Text = Application.Current?.Resources["OnboardingDownloading"] as string ?? "";
        try
        {
            await _download.DownloadAsync();
        }
        catch (OperationCanceledException)
        {
            // bỏ qua
        }
        finally
        {
            if (!_download.IsCompleted)
                DownloadBtn.IsEnabled = true;
        }
    }

    private async void OnResumeClicked(object? sender, EventArgs e)
    {
        ResumeBtn.IsVisible = false;
        await _download.DownloadAsync();
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        var title = Application.Current?.Resources["OnboardingSkipWarnTitle"] as string ?? "";
        var msg = Application.Current?.Resources["OnboardingSkipWarnMessage"] as string ?? "";
        var cancel = Application.Current?.Resources["OnboardingSkipWarnCancel"] as string ?? "";
        var ok = Application.Current?.Resources["OnboardingSkipWarnOk"] as string ?? "";
        var confirm = await DisplayAlert(title, msg, ok, cancel);
        if (!confirm)
            return;
        OnboardingNavigationHelper.CompleteToShell(_sp);
    }

    private void OnEnterAppClicked(object? sender, EventArgs e)
    {
        OnboardingNavigationHelper.CompleteToShell(_sp);
    }
}

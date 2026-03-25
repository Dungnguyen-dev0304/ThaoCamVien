using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class StoryAudioPage : ContentPage
{
    private readonly AudioService _audioService;
    private Poi? _currentPoi;
    private bool _isSliderDragging = false;

    public StoryAudioPage(AudioService audioService)
    {
        InitializeComponent();
        _audioService = audioService;
        _audioService.PlaybackStateChanged += OnPlaybackStateChanged;
        _audioService.ProgressChanged += OnProgressChanged;
    }

    /// <summary>
    /// Gọi từ MapPage / QrPage / NumpadPage trước khi navigate
    /// </summary>
    public void LoadPoi(Poi poi)
    {
        _currentPoi = poi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_currentPoi == null) return;

        UpdateUiWithPoi(_currentPoi);
        await StartAudioAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _audioService.StopAsync();
        UpdatePlayPauseButton(false);
    }

    private void UpdateUiWithPoi(Poi poi)
    {
        PoiNameLabel.Text = poi.Name;
        PoiDescTitleLabel.Text = $"Về {poi.Name}";
        PoiDescLabel.Text = poi.Description ?? "Đang tải thông tin...";
        PoiSubtitleLabel.Text = $"Thảo Cầm Viên Sài Gòn  •  Mã: {poi.PoiId:D3}";

        // Ảnh thumbnail (local hoặc URL)
        if (!string.IsNullOrEmpty(poi.ImageThumbnail))
        {
            PoiImage.Source = poi.ImageThumbnail.StartsWith("http")
                ? ImageSource.FromUri(new Uri(poi.ImageThumbnail))
                : ImageSource.FromFile(poi.ImageThumbnail);
        }
    }

    private async Task StartAudioAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            // AudioService tự lấy URL từ PoiMedium qua DatabaseService
            await _audioService.PlayPoiAudioAsync(_currentPoi!.PoiId);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Không có audio",
                $"Chưa có file thuyết minh cho khu vực này.\n({ex.Message})", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnPlayPauseClicked(object sender, TappedEventArgs e)
    {
        if (_currentPoi == null) return;

        if (_audioService.IsPlaying)
            _audioService.Pause();
        else if (_audioService.Duration == 0)
            await StartAudioAsync();
        else
            _audioService.Resume();
    }

    private void OnRewindClicked(object sender, EventArgs e)
        => _audioService.SeekTo(Math.Max(0, _audioService.CurrentPosition - 10));

    private void OnForwardClicked(object sender, EventArgs e)
        => _audioService.SeekTo(Math.Min(_audioService.Duration, _audioService.CurrentPosition + 10));

    private void OnSliderDragCompleted(object sender, EventArgs e)
    {
        _isSliderDragging = false;
        var seekTo = (ProgressSlider.Value / 100.0) * _audioService.Duration;
        _audioService.SeekTo(seekTo);
    }

    // === Callbacks từ AudioService ===

    private void OnPlaybackStateChanged(object? sender, bool isPlaying)
        => MainThread.BeginInvokeOnMainThread(() => UpdatePlayPauseButton(isPlaying));

    private void OnProgressChanged(object? sender, double currentPos)
    {
        if (_isSliderDragging) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var duration = _audioService.Duration;
            if (duration > 0)
                ProgressSlider.Value = (currentPos / duration) * 100;

            CurrentTimeLabel.Text = FormatTime(currentPos);
            TotalTimeLabel.Text = FormatTime(duration);
        });
    }

    private void UpdatePlayPauseButton(bool isPlaying)
        => PlayPauseIcon.Source = isPlaying ? "icon_pause_white.png" : "icon_play_white.png";

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
    }

    private async void OnBackClicked(object sender, EventArgs e)
        => await Navigation.PopAsync();

    ~StoryAudioPage()
    {
        _audioService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioService.ProgressChanged -= OnProgressChanged;
    }
}

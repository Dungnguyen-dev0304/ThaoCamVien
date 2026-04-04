using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class StoryAudioPage : ContentPage
{
    private readonly AudioService _audioService;
    private readonly NarrationEngine _narrationEngine;
    private readonly DatabaseService _databaseService;

    private Poi? _currentPoi;
    private bool _isSliderDragging = false;

    public StoryAudioPage(AudioService audioService, NarrationEngine narrationEngine, DatabaseService databaseService)
    {
        InitializeComponent();
        _audioService = audioService;
        _narrationEngine = narrationEngine;
        _databaseService = databaseService;

        _audioService.PlaybackStateChanged += OnPlaybackStateChanged;
        _audioService.ProgressChanged += OnProgressChanged;
    }

    public void LoadPoi(Poi poi) => _currentPoi = poi;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_currentPoi == null) return;
        UpdateUiWithPoi(_currentPoi);
        await StartNarrationAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _narrationEngine.StopCurrentNarrationAsync();
        UpdatePlayPauseButton(false);
    }

    // Cập nhật toàn bộ UI với dữ liệu động từ POI
    private void UpdateUiWithPoi(Poi poi)
    {
        var lang = _databaseService.CurrentLanguage;

        PoiNameLabel.Text = poi.Name;
        PoiSubtitleLabel.Text = $"Thảo Cầm Viên Sài Gòn • Mã: TCVN-{poi.PoiId:D3}";
        PoiDescTitleLabel.Text = GetStoryTitle(poi.Name, lang);
        PoiDescLabel.Text = poi.Description ?? "Chưa có thông tin chi tiết.";

        // Intro ngắn: 120 ký tự đầu
        ShortIntroLabel.Text = (poi.Description?.Length ?? 0) > 120
            ? poi.Description![..120] + "..."
            : poi.Description ?? "";

        // Ảnh: URL từ server hoặc file local
        PoiImage.Source = !string.IsNullOrEmpty(poi.ImageThumbnail)
            ? (poi.ImageThumbnail.StartsWith("http")
                ? ImageSource.FromUri(new Uri(poi.ImageThumbnail))
                : ImageSource.FromFile(poi.ImageThumbnail))
            : "placeholder_animal.png";

        // Reset tiến trình
        ProgressSlider.Value = 0;
        CurrentTimeLabel.Text = "00:00";
        TotalTimeLabel.Text = "00:00";
    }

    private static string GetStoryTitle(string name, string lang) => lang switch
    {
        "en" => $"{name} Story",
        "th" => $"เรื่องราวของ{name}",
        "id" => $"Kisah {name}",
        "ms" => $"Kisah {name}",
        "km" => $"រឿងរ៉ាវ{name}",
        _ => $"Câu chuyện {name}",
    };

    private async Task StartNarrationAsync()
    {
        if (_currentPoi == null) return;
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;
            // NarrationEngine tự quyết: MP3 hay TTS, đúng ngôn ngữ
            await _narrationEngine.PlayNarrativeAsync(_currentPoi, forcePlay: true);
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
        if (_audioService.IsPlaying) _audioService.Pause();
        else if (_audioService.Duration == 0) await StartNarrationAsync();
        else _audioService.Resume();
    }

    private void OnRewindClicked(object sender, EventArgs e)
        => _audioService.SeekTo(Math.Max(0, _audioService.CurrentPosition - 10));

    private void OnForwardClicked(object sender, EventArgs e)
        => _audioService.SeekTo(Math.Min(_audioService.Duration, _audioService.CurrentPosition + 10));

    private void OnSliderDragCompleted(object sender, EventArgs e)
    {
        _isSliderDragging = false;
        _audioService.SeekTo((ProgressSlider.Value / 100.0) * _audioService.Duration);
    }

    private void OnPlaybackStateChanged(object? sender, bool isPlaying)
        => MainThread.BeginInvokeOnMainThread(() => UpdatePlayPauseButton(isPlaying));

    private void OnProgressChanged(object? sender, double currentPos)
    {
        if (_isSliderDragging) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var duration = _audioService.Duration;
            if (duration > 0) ProgressSlider.Value = (currentPos / duration) * 100;
            CurrentTimeLabel.Text = FormatTime(currentPos);
            TotalTimeLabel.Text = FormatTime(duration);
        });
    }

    private void UpdatePlayPauseButton(bool isPlaying)
        => PlayPauseIcon.Source = isPlaying ? "icon_pause_dark.png" : "icon_play_dark.png";

    private static string FormatTime(double s)
    {
        var ts = TimeSpan.FromSeconds(s);
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

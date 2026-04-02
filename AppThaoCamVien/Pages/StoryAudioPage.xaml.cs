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

    public void LoadPoi(Poi poi)
    {
        _currentPoi = poi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_currentPoi == null) return;

        // ĐÃ SỬA Ở ĐÂY: Gọi đúng tên hàm Async và thêm await
        await UpdateUiWithPoiAsync(_currentPoi);

        await StartAudioAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _audioService.StopAsync();
        UpdatePlayPauseButton(false);
    }

    // HÀM MỚI: Đã đổi tên thành Async và tự động dịch
    private async Task UpdateUiWithPoiAsync(Poi poi)
    {
        // 1. Hiển thị tạm tiếng Việt để UI không bị trống trong lúc chờ dịch
        PoiNameLabel.Text = poi.Name;
        PoiDescTitleLabel.Text = $"Câu chuyện {poi.Name}";
        PoiSubtitleLabel.Text = $"Mã định danh: TCVN-{poi.PoiId:D3}";
        PoiDescLabel.Text = "Đang dịch nội dung... / Translating...";
        ShortIntroLabel.Text = "";

        // Load ảnh
        if (!string.IsNullOrEmpty(poi.ImageThumbnail))
            PoiImage.Source = poi.ImageThumbnail.StartsWith("http") ? ImageSource.FromUri(new Uri(poi.ImageThumbnail)) : poi.ImageThumbnail;
        else
            PoiImage.Source = "placeholder_animal.png";

        // 2. GỌI GOOGLE DỊCH
        var translator = IPlatformApplication.Current.Services.GetService<AutoTranslateService>();
        var databaseService = IPlatformApplication.Current.Services.GetService<DatabaseService>();
        string currentLang = databaseService?.CurrentLanguage ?? "vi";

        string translatedName = poi.Name;
        string translatedDesc = poi.Description ?? "Chưa có thông tin.";

        if (translator != null && currentLang != "vi")
        {
            translatedName = await translator.TranslateAsync(poi.Name, currentLang);
            translatedDesc = await translator.TranslateAsync(poi.Description ?? "", currentLang);
        }

        // 3. Cập nhật lại UI sau khi có bản dịch (Ép chạy trên luồng chính)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PoiNameLabel.Text = translatedName;
            PoiDescTitleLabel.Text = currentLang == "vi" ? $"Câu chuyện {translatedName}" : $"{translatedName} Story";
            PoiDescLabel.Text = translatedDesc;

            ShortIntroLabel.Text = translatedDesc.Length > 100 ? translatedDesc.Substring(0, 100) + "..." : translatedDesc;
        });
    }

    private async Task StartAudioAsync()
    {
        try
        {
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            await _audioService.PlayPoiAudioAsync(_currentPoi!.PoiId);
        }
        catch (Exception ex)
        {
            // Tắt popup báo lỗi đi để TTS tự động đọc mà không làm phiền người dùng
            System.Diagnostics.Debug.WriteLine($"[StoryAudioPage] Chuyển sang TTS vì: {ex.Message}");
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
    {
        PlayPauseIcon.Source = isPlaying ? "icon_pause_dark.png" : "icon_play_dark.png";
    }

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
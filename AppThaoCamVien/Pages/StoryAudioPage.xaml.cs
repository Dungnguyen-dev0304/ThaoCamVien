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
        PoiSubtitleLabel.Text = $"Mã định danh: TCVN-{poi.PoiId:D3}";
        PoiDescTitleLabel.Text = $"Câu chuyện {poi.Name}";

        // Đổ dữ liệu Description từ Database vào UI
        PoiDescLabel.Text = string.IsNullOrEmpty(poi.Description) ? "Đang cập nhật nội dung chi tiết..." : poi.Description;

        // Trích một đoạn ngắn gọn cho phần Intro (Lấy 100 ký tự đầu tiên nếu có)
        if (!string.IsNullOrEmpty(poi.Description))
        {
            ShortIntroLabel.Text = poi.Description.Length > 100
                ? poi.Description.Substring(0, 100) + "..."
                : poi.Description;
        }
        else
        {
            ShortIntroLabel.Text = "Cùng lắng nghe câu chuyện thú vị về địa điểm này nhé.";
        }

        // Xử lý hình ảnh từ Database (Ví dụ trong DB đang lưu là 'ha_ma.jpg')
        if (!string.IsNullOrEmpty(poi.ImageThumbnail))
        {
            if (poi.ImageThumbnail.StartsWith("http"))
            {
                // Nếu lưu link mạng
                PoiImage.Source = ImageSource.FromUri(new Uri(poi.ImageThumbnail));
            }
            else
            {
                // Nếu lưu tên file local, MAUI sẽ tự tìm trong thư mục Resources/Images
                PoiImage.Source = poi.ImageThumbnail;
            }
        }
        else
        {
            // Ảnh mặc định nếu DB bị null
            PoiImage.Source = "placeholder_animal.png";
        }
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
            await DisplayAlert("Không có audio", $"Chưa có file thuyết minh cho khu vực này.\n({ex.Message})", "OK");
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

    // Các nút Tua đi / Tua lại (Tôi đã ẩn icon đi trong UI để giao diện gọn giống ảnh mẫu, 
    // nhưng bạn có thể thêm lại ImageButton gọi hàm này nếu muốn)
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
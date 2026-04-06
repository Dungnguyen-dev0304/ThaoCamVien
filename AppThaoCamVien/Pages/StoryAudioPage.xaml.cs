using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

/// <summary>
/// StoryAudioPage — FIX toàn bộ crash:
///
/// 1. KHÔNG await trực tiếp narration trong OnAppearing (block UI thread)
/// 2. TTS chạy trên background thread với Task.Run + delay
/// 3. AudioService lỗi → bắt exception, fallback TTS thay vì crash
/// 4. Tất cả UI update phải qua MainThread.BeginInvokeOnMainThread
/// </summary>
public partial class StoryAudioPage : ContentPage
{
    private readonly AudioService _audio;
    private readonly NarrationEngine _narration;
    private readonly DatabaseService _db;

    private Poi? _poi;
    private bool _dragging;
    private bool _isNarrating;

    public StoryAudioPage(AudioService audio, NarrationEngine narration, DatabaseService db)
    {
        InitializeComponent();
        _audio = audio;
        _narration = narration;
        _db = db;

        _audio.PlaybackStateChanged += OnStateChanged;
        _audio.ProgressChanged += OnProgress;
    }

    public void LoadPoi(Poi poi)
    {
        _poi = poi;
        // Render ngay nếu trang đã visible (tránh trường hợp LoadPoi gọi sau OnAppearing)
        if (IsLoaded) RenderPoi(poi);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_poi == null) return;

        RenderPoi(_poi);

        // QUAN TRỌNG: Phát audio/TTS hoàn toàn trên background thread
        // Không await ở đây để không block UI thread → tránh ANR crash trên Android
        _ = StartNarrationSafeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isNarrating = false;

        // Dừng audio ngầm, không await để không block
        _ = Task.Run(async () =>
        {
            try { await _narration.StopAsync(); }
            catch { /* ignore */ }
        });

        MainThread.BeginInvokeOnMainThread(() => UpdateBtn(false));
    }

    // ── Render dữ liệu động từ POI ───────────────────────────────────────
    private void RenderPoi(Poi poi)
    {
        // Phải chạy trên MainThread nếu được gọi từ background
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => RenderPoi(poi));
            return;
        }

        var lang = _db.CurrentLanguage;

        PoiNameLabel.Text = poi.Name ?? "---";
        PoiSubtitleLabel.Text = $"Thảo Cầm Viên • TCVN-{poi.PoiId:D3}";

        PoiDescTitleLabel.Text = lang switch
        {
            "en" => $"{poi.Name} Story",
            "th" => $"เรื่องของ{poi.Name}",
            "id" => $"Kisah {poi.Name}",
            "ms" => $"Kisah {poi.Name}",
            "km" => $"រឿង{poi.Name}",
            _ => $"Câu chuyện {poi.Name}",
        };

        var desc = poi.Description ?? "Chưa có thông tin chi tiết.";
        PoiDescLabel.Text = desc;
        ShortIntroLabel.Text = desc.Length > 120 ? desc[..120] + "..." : desc;

        // Ảnh: an toàn với try-catch
        try
        {
            if (!string.IsNullOrEmpty(poi.ImageThumbnail))
                PoiImage.Source = poi.ImageThumbnail.StartsWith("http")
                    ? ImageSource.FromUri(new Uri(poi.ImageThumbnail))
                    : ImageSource.FromFile(poi.ImageThumbnail);
            else
                PoiImage.Source = "placeholder_animal.png";
        }
        catch
        {
            PoiImage.Source = "placeholder_animal.png";
        }

        ProgressSlider.Value = 0;
        CurrentTimeLabel.Text = "00:00";
        TotalTimeLabel.Text = "00:00";
    }

    // ── Phát thuyết minh AN TOÀN ─────────────────────────────────────────
    private async Task StartNarrationSafeAsync()
    {
        if (_poi == null || _isNarrating) return;
        _isNarrating = true;

        try
        {
            // Hiện loading
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
            });

            // Delay nhỏ để UI render xong và TTS engine sẵn sàng
            await Task.Delay(300);

            if (!_isNarrating) return; // Đã navigate đi

            // NarrationEngine tự quyết: MP3 có → phát MP3, không có → TTS
            // forcePlay=true: bỏ qua cooldown khi user chủ động vào trang
            await _narration.PlayAsync(_poi, forcePlay: true);
        }
        catch (Exception ex)
        {
            // KHÔNG crash app, chỉ log lỗi
            System.Diagnostics.Debug.WriteLine($"[StoryPage] Narration error: {ex.Message}");
        }
        finally
        {
            _isNarrating = false;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            });
        }
    }

    // ── Player Controls ──────────────────────────────────────────────────
    private async void OnPlayPauseClicked(object sender, TappedEventArgs e)
    {
        if (_poi == null) return;

        try
        {
            if (_audio.IsPlaying)
            {
                _audio.Pause();
            }
            else if (_audio.Duration == 0)
            {
                // Chưa load audio → phát lại
                await StartNarrationSafeAsync();
            }
            else
            {
                _audio.Resume();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoryPage] PlayPause error: {ex.Message}");
        }
    }

    private void OnRewindClicked(object sender, EventArgs e)
    {
        try { _audio.SeekTo(Math.Max(0, _audio.CurrentPosition - 10)); } catch { }
    }

    private void OnForwardClicked(object sender, EventArgs e)
    {
        try { _audio.SeekTo(Math.Min(_audio.Duration, _audio.CurrentPosition + 10)); } catch { }
    }

    private void OnSliderDragStarted(object sender, EventArgs e) => _dragging = true;

    private void OnSliderDragCompleted(object sender, EventArgs e)
    {
        _dragging = false;
        try { _audio.SeekTo((ProgressSlider.Value / 100.0) * _audio.Duration); } catch { }
    }

    // ── AudioService Events ──────────────────────────────────────────────
    private void OnStateChanged(object? s, bool playing)
        => MainThread.BeginInvokeOnMainThread(() => UpdateBtn(playing));

    private void OnProgress(object? s, double pos)
    {
        if (_dragging) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var dur = _audio.Duration;
                if (dur > 0) ProgressSlider.Value = (pos / dur) * 100;
                CurrentTimeLabel.Text = Fmt(pos);
                TotalTimeLabel.Text = Fmt(dur);
            }
            catch { /* ignore UI update errors */ }
        });
    }

    private void UpdateBtn(bool playing)
        => PlayPauseIcon.Source = playing ? "icon_pause_dark.png" : "icon_play_dark.png";

    private static string Fmt(double s)
    {
        var t = TimeSpan.FromSeconds(s < 0 ? 0 : s);
        return $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    ~StoryAudioPage()
    {
        try
        {
            _audio.PlaybackStateChanged -= OnStateChanged;
            _audio.ProgressChanged -= OnProgress;
        }
        catch { }
    }
}

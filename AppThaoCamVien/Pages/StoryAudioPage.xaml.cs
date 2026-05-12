using AppThaoCamVien.Services;
using AppThaoCamVien.ViewModels;
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
    private readonly StoryAudioViewModel _vm;
    private readonly IServiceProvider _sp;
    private readonly PaymentApiService _paymentApi;

    private Poi? _poi;
    private bool _dragging;
    private bool _isNarrating;
    private bool _premiumCheckDone;
    private bool _premiumGated;  // true = đang khóa, chưa cho phát

    public StoryAudioPage(AudioService audio, NarrationEngine narration, DatabaseService db, StoryAudioViewModel vm, IServiceProvider sp, PaymentApiService paymentApi)
    {
        InitializeComponent();
        _audio = audio;
        _narration = narration;
        _db = db;
        _vm = vm;
        _sp = sp;
        _paymentApi = paymentApi;
        BindingContext = _vm;

        _audio.PlaybackStateChanged += OnStateChanged;
        _audio.ProgressChanged += OnProgress;
        _narration.QueueChanged += OnNarrationQueueChanged;
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
        UpdateQueueHintUI();
        try
        {
            _vm.SetPoiContext(_poi.PoiId);
            _ = _vm.SafeReloadAsync();
        }
        catch { /* ignore UI analytics load errors */ }

        // ── KHÓA AUDIO MẶC ĐỊNH ngay khi page xuất hiện ───────────────────
        // Đặt gate = true trước khi check để chặn mọi audio đang phát từ
        // NarrationEngine/geofencing. Sau khi xác minh Premium status,
        // mới mở khóa nếu POI free hoặc device đã mua.
        _premiumGated = true;
        StopAllAudioImmediate();

        // Chạy check Premium ở background, không block UI
        _ = Task.Run(CheckPremiumAndStartAsync);
    }

    /// <summary>
    /// Dừng NGAY toàn bộ audio + narration khi page mở.
    /// Tránh tình trạng audio đang phát từ trang trước hoặc geofencing.
    /// </summary>
    private void StopAllAudioImmediate()
    {
        try
        {
            // Stop AudioService trên main thread (không await)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { _audio.Pause(); } catch { }
                try { UpdateBtn(false); } catch { }
            });

            // Stop NarrationEngine queue ngầm
            _ = Task.Run(async () =>
            {
                try { await _narration.StopAsync(); } catch { }
            });
        }
        catch { /* tuyệt đối không throw từ OnAppearing */ }
    }

    /// <summary>
    /// Kiểm tra premium → nếu chưa mua thì show gate, nếu đã mua hoặc free thì phát audio.
    /// Chạy nền để không block UI.
    /// </summary>
    private async Task CheckPremiumAndStartAsync()
    {
        if (_poi == null) return;

        bool gated = false;
        try
        {
            var info = await _paymentApi.CheckPremiumAsync(_poi.PoiId);
            if (info != null && info.isPremium)
            {
                // POI premium → kiểm tra device đã mua chưa
                var hasAccess = await _paymentApi.HasAccessAsync(_poi.PoiId);
                gated = !hasAccess;

                if (info.price > 0)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        GatePriceLabel.Text = $"{info.price:#,##0} ₫";
                        GatePoiNameLabel.Text = info.name ?? _poi.Name ?? "—";
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoryPage] Premium check error: {ex.Message}");
            // Lỗi mạng → fallback: cho phép xem (không khóa, để app vẫn chạy)
            gated = false;
        }

        _premiumCheckDone = true;
        _premiumGated = gated;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PremiumGateOverlay.IsVisible = gated;
        });

        if (gated)
        {
            // Vẫn bị khóa → đảm bảo audio hoàn toàn dừng (lần 2 cho chắc)
            StopAllAudioImmediate();
        }
        else
        {
            // Không khóa → phát thuyết minh ngay
            _ = StartNarrationSafeAsync();
        }
    }

    /// <summary>User nhấn nút "Mở khóa ngay" trên Premium Gate.</summary>
    private async void OnUnlockClicked(object sender, EventArgs e)
    {
        if (_poi == null) return;

        try
        {
            var page = _sp.GetRequiredService<PaymentPage>();
            page.SetPoi(_poi);
            await Navigation.PushAsync(page);

            var ok = await page.WaitForResultAsync();
            if (ok)
            {
                // Thanh toán thành công → ẩn gate và phát audio
                _premiumGated = false;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PremiumGateOverlay.IsVisible = false;
                });
                _ = StartNarrationSafeAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không mở được trang thanh toán: {ex.Message}", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isNarrating = false;
        QueueHintPanel.IsVisible = false;

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
            
            _ => $"Câu chuyện {poi.Name}",
        };

        var desc = poi.Description ?? "Chưa có thông tin chi tiết.";
        PoiDescLabel.Text = desc;
        //ShortIntroLabel.Text = desc.Length > 120 ? desc[..120] + "..." : desc;

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
        if (_premiumGated)
        {
            System.Diagnostics.Debug.WriteLine("[StoryPage] Narration blocked: premium gated");
            return;
        }
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
        if (_premiumGated)
        {
            await DisplayAlert("Nội dung Premium",
                "Vui lòng mở khóa nội dung này để nghe.", "OK");
            return;
        }

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

    // ── Queue UX (Skip + "Tiếp theo") ───────────────────────────────────
    private void OnNarrationQueueChanged(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(UpdateQueueHintUI);

    private void UpdateQueueHintUI()
    {
        try
        {
            var queued = _narration.GetQueueSnapshot();
            if (queued.Count == 0)
            {
                QueueHintPanel.IsVisible = false;
                return;
            }

            var next = queued[0];
            QueueNextPoiLabel.Text = next.Name ?? $"POI #{next.PoiId}";
            QueueHintPanel.IsVisible = true;
        }
        catch
        {
            QueueHintPanel.IsVisible = false;
        }
    }

    private async void OnQueueSkipTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await _narration.SkipAsync();
            // Panel sẽ tự ẩn/đổi theo QueueChanged.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[StoryPage] Skip error: {ex.Message}");
        }
    }

    private static string Fmt(double s)
    {
        var t = TimeSpan.FromSeconds(s < 0 ? 0 : s);
        return $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnNavigateToPoiClicked(object sender, EventArgs e)
    {
        try
        {
            if (_poi == null) return;
            var map = _sp.GetRequiredService<MapPage>();
            map.FocusPoi(_poi);
            await Navigation.PushAsync(map);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }

    ~StoryAudioPage()
    {
        try
        {
            _audio.PlaybackStateChanged -= OnStateChanged;
            _audio.ProgressChanged -= OnProgress;
            _narration.QueueChanged -= OnNarrationQueueChanged;
        }
        catch { }
    }
}

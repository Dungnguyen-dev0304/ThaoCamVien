using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class HomePage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly IServiceProvider _sp;
    private readonly HomePageViewModel _vm; // existing VM (API-backed)
    private readonly ApiService _api;
    private int _langIdx = 0;
    private AudioService? _audio;
    private NarrationEngine? _narration;
    private IDispatcherTimer? _miniTimer;

    public HomePage(HomePageViewModel vm, DatabaseService db, IServiceProvider sp)
    {
        InitializeComponent();
        _vm = vm;
        _db = db;
        _sp = sp;
        _api = _sp.GetRequiredService<ApiService>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            // Sync ngôn ngữ UI + data sync API-first
            var cur = LanguageManager.Current;
            _langIdx = LanguageManager.Languages.FindIndex(l => l.Code == cur);
            if (_langIdx < 0) _langIdx = 0;
            UpdateLangBtn();
            await _vm.SafeReloadAsync();

            _audio ??= _sp.GetService<AudioService>();
            _narration ??= _sp.GetService<NarrationEngine>();
            EnsureMiniPlayerTimer();
        }
        catch (Exception ex)
        {
            // Không crash — giữ màn hình ổn định
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnAppearing error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { _miniTimer?.Stop(); } catch { }
    }

    private async void OnLangTapped(object sender, TappedEventArgs e)
    {
        _langIdx = (_langIdx + 1) % LanguageManager.Languages.Count;
        var (code, _, _) = LanguageManager.Languages[_langIdx];
        _db.CurrentLanguage = code;
        LanguageManager.Apply(code);
        UpdateLangBtn();
        _ = _vm.SafeReloadAsync();
    }

    private void UpdateLangBtn()
    {
        var (_, flag, label) = LanguageManager.Languages[_langIdx];
        // Nhãn nhỏ chỉ là hiển thị — không ảnh hưởng logic ngôn ngữ
        LangLbl.Text = $"{flag} {label}";
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        var page = _sp.GetService<MapPage>();
        if (page != null) await Navigation.PushAsync(page);
    }

    private async void OnOpenAnimalsClicked(object sender, EventArgs e)
    {
        try
        {
            var page = _sp.GetService<AnimalsPage>();
            if (page != null) await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", ex.Message, "OK");
        }
    }

    private async void OnNowSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is HomeCardDto card)
        {
            ((CollectionView)sender).SelectedItem = null;
            await OpenPoi(card.Id);
        }
    }

    private async void OnRecommendedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is HomeCardDto card)
        {
            ((CollectionView)sender).SelectedItem = null;
            await OpenPoi(card.Id);
        }
    }

    private async Task OpenPoi(int poiId)
    {
        try
        {
            var poi = await _db.GetPoiByIdAsync(poiId);
            if (poi == null)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] local miss poiId={poiId} -> API fallback");
                poi = await _api.GetAsync<Poi>($"/api/Pois/{poiId}?lang={LanguageManager.Current}");
                if (poi != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomePage] API fallback hit poiId={poiId}, save cache");
                    await _db.SavePoiAsync(poi);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] local hit poiId={poiId}");
            }
            if (poi == null)
            {
                System.Diagnostics.Debug.WriteLine($"[HomePage] miss both local/api poiId={poiId}");
                await DisplayAlertAsync("Thông báo", "Không tìm thấy điểm tham quan.", "OK");
                return;
            }

            var page = _sp.GetRequiredService<StoryAudioPage>();
            page.LoadPoi(poi);
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", ex.Message, "OK");
        }
    }

    private async void OnNearbySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.CurrentSelection?.FirstOrDefault() is NearbyPlaceCard card)
            {
                ((CollectionView)sender).SelectedItem = null;
                await OpenPoi(card.PoiId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Nearby selection error: {ex.Message}");
        }
    }

    private async void OnContinueListeningTapped(object sender, TappedEventArgs e)
        => await PlayOrToggleAsync(_vm.ContinueListening.PoiId, openDetailPage: true);

    private async void OnMiniPlayerTapped(object sender, TappedEventArgs e)
        => await PlayOrToggleAsync(_vm.MiniPlayer.PoiId, openDetailPage: false);

    private async Task PlayOrToggleAsync(int poiId, bool openDetailPage)
    {
        try
        {
            _audio ??= _sp.GetService<AudioService>();
            _narration ??= _sp.GetService<NarrationEngine>();

            if (_audio == null || _narration == null)
                return;

            // Nếu đang phát MP3 thì toggle ngay, không làm user bị gián đoạn.
            if (_audio.IsPlaying)
            {
                _audio.Pause();
                return;
            }

            // Nếu đã có duration rồi (đang paused) thì resume.
            if (_audio.Duration > 0)
            {
                _audio.Resume();
                return;
            }

            // Chưa có track -> phát mới. UX: nếu user bấm Continue Listening thì mở chi tiết + auto-play.
            var poi = await _db.GetPoiByIdAsync(poiId);
            if (poi == null) return;

            if (openDetailPage)
            {
                await OpenPoi(poiId); // StoryAudioPage sẽ tự auto-play narration
                return;
            }

            // Mini player: phát nền ngay tại Home (không navigate)
            await _narration.PlayAsync(poi, forcePlay: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] PlayOrToggle error: {ex}");
        }
    }

    private void EnsureMiniPlayerTimer()
    {
        if (_miniTimer != null) return;
        _miniTimer = Dispatcher.CreateTimer();
        _miniTimer.Interval = TimeSpan.FromMilliseconds(500);
        _miniTimer.Tick += (_, _) =>
        {
            try
            {
                if (_audio == null) return;

                var dur = _audio.Duration;
                var pos = _audio.CurrentPosition;
                _vm.UpdatePlaybackUi(pos, dur, _audio.IsPlaying);
            }
            catch { }
        };
        _miniTimer.Start();
    }
}


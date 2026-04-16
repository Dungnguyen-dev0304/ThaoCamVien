using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels.Core;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AppThaoCamVien.ViewModels;

public sealed class HomePageViewModel : BaseViewModel
{
    private readonly ApiService _api;
    private readonly DatabaseService _db;
    private readonly LocationService _location;

    private HomeFeedDto _feed = new();

    private ContinueListeningCard _continueListening = new(
        PoiId: 0,
        Title: "",
        Subtitle: "",
        ImageUrl: "",
        Progress01: 0,
        ElapsedLabel: "00:00",
        DurationLabel: "00:00",
        IsPlaying: false);
    public ContinueListeningCard ContinueListening
    {
        get => _continueListening;
        private set => SetProperty(ref _continueListening, value);
    }

    public ObservableCollection<NearbyPlaceCard> NearbyPlaces { get; } = new();

    private MiniPlayerCard _miniPlayer = new(
        PoiId: 0,
        Title: "",
        Subtitle: "",
        ImageUrl: "",
        Progress01: 0,
        ElapsedLabel: "00:00",
        DurationLabel: "00:00",
        IsPlaying: false);
    public MiniPlayerCard MiniPlayer
    {
        get => _miniPlayer;
        private set => SetProperty(ref _miniPlayer, value);
    }

    private bool _isOffline;
    public bool IsOffline
    {
        get => _isOffline;
        private set => SetProperty(ref _isOffline, value);
    }

    private string _offlineMessage = string.Empty;
    public string OfflineMessage
    {
        get => _offlineMessage;
        private set => SetProperty(ref _offlineMessage, value);
    }

    public HomeFeedDto Feed
    {
        get => _feed;
        private set => SetProperty(ref _feed, value);
    }

    private string _headerTitle = "";
    public string HeaderTitle
    {
        get => _headerTitle;
        private set => SetProperty(ref _headerTitle, value);
    }

    private string _headerSubtitle = "";
    public string HeaderSubtitle
    {
        get => _headerSubtitle;
        private set => SetProperty(ref _headerSubtitle, value);
    }

    private string _continueListeningKicker = "";
    public string ContinueListeningKicker
    {
        get => _continueListeningKicker;
        private set => SetProperty(ref _continueListeningKicker, value);
    }

    private string _nearbyPlacesTitle = "";
    public string NearbyPlacesTitle
    {
        get => _nearbyPlacesTitle;
        private set => SetProperty(ref _nearbyPlacesTitle, value);
    }

    private string _startTourTitle = "";
    public string StartTourTitle
    {
        get => _startTourTitle;
        private set => SetProperty(ref _startTourTitle, value);
    }

    private string _startTourButtonText = "";
    public string StartTourButtonText
    {
        get => _startTourButtonText;
        private set => SetProperty(ref _startTourButtonText, value);
    }

    private string _startTourBackgroundImageUrl = "";
    public string StartTourBackgroundImageUrl
    {
        get => _startTourBackgroundImageUrl;
        private set => SetProperty(ref _startTourBackgroundImageUrl, value);
    }

    public HomePageViewModel(ApiService api, DatabaseService db, LocationService location)
    {
        _api = api;
        _db = db;
        _location = location;
        EmptyMessage = "Hiện chưa có nội dung phù hợp.";
    }

    public void UpdatePlaybackUi(double positionSeconds, double durationSeconds, bool isPlaying)
    {
        var p01 = durationSeconds > 0 ? Math.Clamp(positionSeconds / durationSeconds, 0, 1) : 0;

        MiniPlayer = MiniPlayer with
        {
            Progress01 = p01,
            ElapsedLabel = Fmt(positionSeconds),
            DurationLabel = Fmt(durationSeconds),
            IsPlaying = isPlaying
        };

        ContinueListening = ContinueListening with
        {
            Progress01 = p01,
            ElapsedLabel = Fmt(positionSeconds),
            DurationLabel = Fmt(durationSeconds),
            IsPlaying = isPlaying
        };
    }

    private static string Fmt(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds < 0 ? 0 : seconds);
        return $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";
    }

    protected override async Task LoadAsync()
    {
        var lang = LanguageManager.Current;
        _db.CurrentLanguage = lang;

        await _db.SyncDataFromApiAsync();
        var feed = await _api.GetAsync<HomeFeedDto>($"{ApiEndpoints.HomeFeed}?lang={lang}");

        if (feed != null)
        {
            Feed = feed;
            IsOffline = false;
            OfflineMessage = string.Empty;

            HeaderTitle = feed.HeaderTitle;
            HeaderSubtitle = feed.HeaderSubtitle;
            ContinueListeningKicker = feed.ContinueListeningKicker;
            NearbyPlacesTitle = feed.NearbyPlacesTitle;
            StartTourTitle = feed.StartTourTitle;
            StartTourButtonText = feed.StartTourButtonText;
            StartTourBackgroundImageUrl = feed.StartTourBackgroundImageUrl;
        }
        else
        {
            IsOffline = true;
            OfflineMessage = "Đang sử dụng dữ liệu offline.";

            // Cung cấp text mặc định khi API không khả dụng
            HeaderTitle = lang == "en" ? "Welcome to Saigon Zoo!" : "Chào mừng đến Thảo Cầm Viên!";
            HeaderSubtitle = lang == "en" ? "Explore wildlife" : "Khám phá thiên nhiên hoang dã";
            ContinueListeningKicker = lang == "en" ? "CONTINUE LISTENING" : "TIẾP TỤC NGHE";
            NearbyPlacesTitle = lang == "en" ? "ANIMALS" : "ĐỘNG VẬT";
            StartTourTitle = lang == "en" ? "START TOUR" : "BẮT ĐẦU HÀNH TRÌNH";
            StartTourButtonText = lang == "en" ? "Start Tour" : "BẮT ĐẦU";
        }

        var lastPoi = await _db.GetLastVisitedPoiAsync();
        if (lastPoi == null)
        {
            var all = await _db.GetAllPoisAsync();
            lastPoi = all.OrderByDescending(p => p.Priority).FirstOrDefault();
        }

        if (lastPoi != null)
        {
            var subtitle = Truncate(lastPoi.Description, 70);
            ContinueListening = ContinueListening with
            {
                PoiId = lastPoi.PoiId,
                Title = lastPoi.Name ?? "",
                Subtitle = subtitle,
                ImageUrl = lastPoi.ImageThumbnail ?? ""
            };

            MiniPlayer = MiniPlayer with
            {
                PoiId = lastPoi.PoiId,
                Title = lastPoi.Name ?? "",
                Subtitle = subtitle,
                ImageUrl = lastPoi.ImageThumbnail ?? ""
            };
        }

        // ==============================================================
        // LOGIC LẤY 5 CON VẬT GẦN NHẤT ĐÃ ĐƯỢC FIX LỖI MÁY ẢO
        // ==============================================================
        try
        {
            var loc = await _location.GetCurrentAsync();

            // NẾU MÁY ẢO KHÔNG CÓ GPS, DÙNG TỌA ĐỘ GIẢ LẬP CỦA THẢO CẦM VIÊN
            double lat = loc?.Latitude ?? 10.78738006;
            double lng = loc?.Longitude ?? 106.70506044;

            var url = $"{ApiEndpoints.NearbyAnimals}?lat={lat}&lng={lng}&radius=5000&lang={lang}";
            System.Diagnostics.Debug.WriteLine($"[API GỌI]: {url}");

            var nearby = await _api.GetAsync<List<NearbyPoiDto>>(url);
            NearbyPlaces.Clear();

            if (nearby != null && nearby.Count > 0)
            {
                var top5Nearby = nearby.OrderBy(x => x.DistanceMeters).Take(5);
                foreach (var n in top5Nearby)
                {
                    NearbyPlaces.Add(new NearbyPlaceCard(
                        PoiId: n.PoiId,
                        Title: n.Name,
                        DistanceLabel: n.DistanceLabel,
                        LocationHint: n.LocationHint,
                        ImageUrl: n.ThumbnailUrl));
                }
            }
            else
            {
                // API TRẢ VỀ RỖNG -> BƠM DATA FALLBACK ĐỂ UI KHÔNG BỊ TRẮNG
                LoadFallbackData();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"🚨 LỖI GỌI API NEARBY: {ex.Message}");
            // LỖI MẠNG HOẶC API -> BƠM DATA FALLBACK ĐỂ UI KHÔNG BỊ TRẮNG
            LoadFallbackData();
        }
        // ==============================================================

        // Luôn hiển thị Success nếu có bất kỳ dữ liệu nào (API hoặc local)
        // Chỉ Empty khi thực sự không có gì để hiển thị
        if (ContinueListening.PoiId == 0 && NearbyPlaces.Count == 0 && feed == null)
        {
            // Thử lần cuối: load từ local DB
            LoadFallbackData();
            // Đợi một chút để fallback load xong
            await Task.Delay(200);
        }

        // Sau fallback, nếu vẫn hoàn toàn trống thì mới Empty
        if (ContinueListening.PoiId == 0 && NearbyPlaces.Count == 0 && feed == null)
        {
            State = UiState.Empty;
            return;
        }

        State = UiState.Success;
    }

    /// <summary>
    /// Fallback: tải danh sách từ SQLite local khi API nearby không khả dụng.
    /// Dùng dữ liệu POI đã sync từ trước để UI không bị trắng.
    /// </summary>
    private async void LoadFallbackData()
    {
        System.Diagnostics.Debug.WriteLine("[HomeVM] Loading fallback from local SQLite...");

        try
        {
            var localPois = await _db.GetAllPoisAsync();
            var top5 = localPois.OrderByDescending(p => p.Priority).Take(5).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                NearbyPlaces.Clear();
                foreach (var p in top5)
                {
                    NearbyPlaces.Add(new NearbyPlaceCard(
                        PoiId: p.PoiId,
                        Title: p.Name ?? "---",
                        DistanceLabel: "THÚ",
                        LocationHint: "Thảo Cầm Viên",
                        ImageUrl: p.ImageThumbnail ?? ""));
                }

                if (NearbyPlaces.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[HomeVM] No local POIs either — UI will be empty.");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeVM] LoadFallbackData error: {ex.Message}");
        }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        if (s.Length <= max) return s;
        return s[..max] + "…";
    }
}

// ── Simple UI models for new HomePage design ────────────────────────
public sealed record HomeQuickAction(string Emoji, string Title, ICommand Command);
public sealed record FeaturedAnimalCard(int Id, string Name, string Subtitle, string ImageUrl);
public sealed record UpcomingEventCard(string Emoji, string Title, string TimeLabel, string Location);

public sealed record NearbyPlaceCard(int PoiId, string Title, string DistanceLabel, string LocationHint, string ImageUrl);

public sealed record ContinueListeningCard(
    int PoiId,
    string Title,
    string Subtitle,
    string ImageUrl,
    double Progress01,
    string ElapsedLabel,
    string DurationLabel,
    bool IsPlaying)
{
    public string PlayGlyph => IsPlaying ? "⏸" : "▶";
}

public sealed record MiniPlayerCard(
    int PoiId,
    string Title,
    string Subtitle,
    string ImageUrl,
    double Progress01,
    string ElapsedLabel,
    string DurationLabel,
    bool IsPlaying)
{
    public string PlayGlyph => IsPlaying ? "⏸" : "▶";
}
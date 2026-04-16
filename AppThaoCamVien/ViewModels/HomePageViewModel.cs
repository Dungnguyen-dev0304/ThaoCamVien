using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels.Core;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace AppThaoCamVien.ViewModels;

public sealed class HomePageViewModel : BaseViewModel
{
    private readonly ApiService _api;
    private readonly DatabaseService _db;
    private readonly LocationService _location;

    // Zoo center coordinates (fallback when GPS unavailable)
    private const double ZooLat = 10.78738006;
    private const double ZooLng = 106.70506044;

    private HomeFeedDto _feed = new();

    private ContinueListeningCard _continueListening = new(
        PoiId: 0, Title: "", Subtitle: "", ImageUrl: "",
        Progress01: 0, ElapsedLabel: "00:00", DurationLabel: "00:00", IsPlaying: false);
    public ContinueListeningCard ContinueListening
    {
        get => _continueListening;
        private set => SetProperty(ref _continueListening, value);
    }

    public ObservableCollection<NearbyPlaceCard> NearbyPlaces { get; } = new();

    private MiniPlayerCard _miniPlayer = new(
        PoiId: 0, Title: "", Subtitle: "", ImageUrl: "",
        Progress01: 0, ElapsedLabel: "00:00", DurationLabel: "00:00", IsPlaying: false);
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
            ElapsedLabel = FormatTime(positionSeconds),
            DurationLabel = FormatTime(durationSeconds),
            IsPlaying = isPlaying
        };

        ContinueListening = ContinueListening with
        {
            Progress01 = p01,
            ElapsedLabel = FormatTime(positionSeconds),
            DurationLabel = FormatTime(durationSeconds),
            IsPlaying = isPlaying
        };
    }

    protected override async Task LoadAsync()
    {
        var lang = LanguageManager.Current;
        _db.CurrentLanguage = lang;

        await _db.SyncDataFromApiAsync();

        var feed = await _api.GetAsync<HomeFeedDto>($"{ApiEndpoints.HomeFeed}?lang={lang}");
        ApplyFeed(feed, lang);

        await LoadContinueListeningAsync();
        await LoadNearbyPlacesAsync(lang);

        // Nếu hoàn toàn trống → fallback local
        if (ContinueListening.PoiId == 0 && NearbyPlaces.Count == 0)
            await LoadFallbackDataAsync(lang);

        State = (ContinueListening.PoiId == 0 && NearbyPlaces.Count == 0 && feed == null)
            ? UiState.Empty
            : UiState.Success;
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void ApplyFeed(HomeFeedDto? feed, string lang)
    {
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
            OfflineMessage = lang == "en" ? "Using offline data." : "Đang sử dụng dữ liệu offline.";
            HeaderTitle = lang == "en" ? "Welcome to Saigon Zoo!" : "Chào mừng đến Thảo Cầm Viên!";
            HeaderSubtitle = lang == "en" ? "Explore wildlife" : "Khám phá thiên nhiên hoang dã";
            ContinueListeningKicker = lang == "en" ? "CONTINUE LISTENING" : "TIẾP TỤC NGHE";
            NearbyPlacesTitle = lang == "en" ? "ANIMALS" : "ĐỘNG VẬT";
            StartTourTitle = lang == "en" ? "START TOUR" : "BẮT ĐẦU HÀNH TRÌNH";
            StartTourButtonText = lang == "en" ? "Start Tour" : "BẮT ĐẦU";
        }
    }

    private async Task LoadContinueListeningAsync()
    {
        var lastPoi = await _db.GetLastVisitedPoiAsync();
        if (lastPoi == null)
        {
            var all = await _db.GetAllPoisAsync();
            lastPoi = all.OrderByDescending(p => p.Priority).FirstOrDefault();
        }

        if (lastPoi == null) return;

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

    private async Task LoadNearbyPlacesAsync(string lang)
    {
        try
        {
            var loc = await _location.GetCurrentAsync();
            double lat = loc?.Latitude ?? ZooLat;
            double lng = loc?.Longitude ?? ZooLng;

            var url = $"{ApiEndpoints.NearbyAnimals}?lat={lat}&lng={lng}&radius=5000&lang={lang}";
            var nearby = await _api.GetAsync<List<NearbyPoiDto>>(url);

            NearbyPlaces.Clear();

            if (nearby is { Count: > 0 })
            {
                foreach (var n in nearby.OrderBy(x => x.DistanceMeters).Take(5))
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
                await LoadFallbackDataAsync(lang);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeVM] Nearby API error: {ex.Message}");
            await LoadFallbackDataAsync(lang);
        }
    }

    /// <summary>
    /// Fallback: tải top 5 POI từ SQLite local khi API không khả dụng.
    /// </summary>
    private async Task LoadFallbackDataAsync(string lang)
    {
        try
        {
            if (NearbyPlaces.Count > 0) return; // Already populated

            var localPois = await _db.GetAllPoisAsync();
            var top5 = localPois.OrderByDescending(p => p.Priority).Take(5).ToList();

            var label = lang == "en" ? "ANIMAL" : "THÚ";
            var hint = lang == "en" ? "Saigon Zoo" : "Thảo Cầm Viên";

            NearbyPlaces.Clear();
            foreach (var p in top5)
            {
                NearbyPlaces.Add(new NearbyPlaceCard(
                    PoiId: p.PoiId,
                    Title: p.Name ?? "---",
                    DistanceLabel: label,
                    LocationHint: hint,
                    ImageUrl: p.ImageThumbnail ?? ""));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HomeVM] Fallback error: {ex.Message}");
        }
    }

    private static string FormatTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }
}

// ── UI record models ────────────────────────────────────────────────

public sealed record HomeQuickAction(string Emoji, string Title, ICommand Command);
public sealed record FeaturedAnimalCard(int Id, string Name, string Subtitle, string ImageUrl);
public sealed record UpcomingEventCard(string Emoji, string Title, string TimeLabel, string Location);
public sealed record NearbyPlaceCard(int PoiId, string Title, string DistanceLabel, string LocationHint, string ImageUrl);

public sealed record ContinueListeningCard(
    int PoiId, string Title, string Subtitle, string ImageUrl,
    double Progress01, string ElapsedLabel, string DurationLabel, bool IsPlaying)
{
    public string PlayGlyph => IsPlaying ? "⏸" : "▶";
}

public sealed record MiniPlayerCard(
    int PoiId, string Title, string Subtitle, string ImageUrl,
    double Progress01, string ElapsedLabel, string DurationLabel, bool IsPlaying)
{
    public string PlayGlyph => IsPlaying ? "⏸" : "▶";
}

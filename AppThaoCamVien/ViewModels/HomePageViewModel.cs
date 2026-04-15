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

    // Always non-null Ä‘á»ƒ XAML binding khÃ´ng gáº·p rá»§i ro null khi 1 request API tháº¥t báº¡i.
    private HomeFeedDto _feed = new();

    // â”€â”€ UI models for â€œHome like reference screenshotâ€ â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
        EmptyMessage = "Hiá»‡n chÆ°a cÃ³ ná»™i dung phÃ¹ há»£p.";
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
        // Sync theo ngÃ´n ngá»¯ hiá»‡n táº¡i (API-first)
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
            OfflineMessage = "KhÃ´ng táº£i Ä‘Æ°á»£c dá»¯ liá»‡u tá»« mÃ¡y chá»§. Vui lÃ²ng kiá»ƒm tra Internet/IP/SSL.";
        }

        // Continue Listening / Mini player: last visit from local DB (synced from API).
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

        // Nearby places from GPS -> API.
        // Nearby places from GPS -> API.
        try
        {
            var loc = await _location.GetCurrentAsync();
            if (loc != null)
            {
                var nearby = await _api.GetAsync<List<NearbyPoiDto>>(
                    $"{ApiEndpoints.NearbyAnimals}?lat={loc.Latitude}&lng={loc.Longitude}&radius=250&lang={lang}");

                NearbyPlaces.Clear();
                if (nearby != null)
                {
                    // THAY ĐỔI Ở ĐÂY: Thêm .Take(5) sau khi đã sắp xếp theo khoảng cách
                    // Việc này đảm bảo UI chỉ render 5 Card, vuốt rất nhẹ mượt và không bị loãng trang chủ
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
            }
        }
        catch
        {
            // Không có GPS/permission → vẫn hiển thị Home.
        }

        if (ContinueListening.PoiId == 0 && NearbyPlaces.Count == 0 && feed == null)
        {
            State = UiState.Empty;
            return;
        }

        State = UiState.Success;
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        if (s.Length <= max) return s;
        return s[..max] + "â€¦";
    }
}

// â”€â”€ Simple UI models for new HomePage design â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    public string PlayGlyph => IsPlaying ? "â¸" : "â–¶";
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
    public string PlayGlyph => IsPlaying ? "â¸" : "â–¶";
}

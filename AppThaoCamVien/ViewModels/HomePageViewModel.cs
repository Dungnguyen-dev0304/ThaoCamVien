using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels.Core;

namespace AppThaoCamVien.ViewModels;

public sealed class HomePageViewModel : BaseViewModel
{
    private readonly ApiService _api;
    private readonly DatabaseService _db;

    private HomeFeedDto? _feed;
    private WeatherDto? _weather;

    public HomeFeedDto? Feed
    {
        get => _feed;
        private set => SetProperty(ref _feed, value);
    }

    public WeatherDto? Weather
    {
        get => _weather;
        private set => SetProperty(ref _weather, value);
    }

    public HomePageViewModel(ApiService api, DatabaseService db)
    {
        _api = api;
        _db = db;
        EmptyMessage = "Hiện chưa có nội dung phù hợp.";
    }

    protected override async Task LoadAsync()
    {
        // Sync theo ngôn ngữ hiện tại (API-first)
        var lang = LanguageManager.Current;
        _db.CurrentLanguage = lang;

        await _db.SyncDataFromApiAsync();
        var feed = await _api.GetAsync<HomeFeedDto>($"{ApiEndpoints.HomeFeed}?lang={lang}");
        var weather = await _api.GetAsync<WeatherDto>($"{ApiEndpoints.Weather}?lang={lang}");

        Feed = feed;
        Weather = weather;

        if (feed == null && weather == null)
        {
            State = UiState.Error;
            ErrorMessage = "Không tải được dữ liệu từ máy chủ.";
            return;
        }

        if ((feed.HappenNow?.Count ?? 0) == 0 && (feed.Recommended?.Count ?? 0) == 0)
        {
            State = UiState.Empty;
            return;
        }

        State = UiState.Success;
    }
}

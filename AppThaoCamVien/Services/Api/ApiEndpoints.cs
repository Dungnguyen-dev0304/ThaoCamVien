namespace AppThaoCamVien.Services.Api;

/// <summary>
/// API endpoint catalog (minimum contracts) for API-first UI.
/// </summary>
public static class ApiEndpoints
{
    public const string HomeFeed = "/api/mobile/home-feed";
    public const string Weather = "/api/mobile/weather/current";
    public const string PoiClusters = "/api/mobile/pois/clusters";
    public const string NearbyAnimals = "/api/mobile/pois/nearby";
    public const string Animals = "/api/mobile/animals";
    public const string Voices = "/api/mobile/tts/voices";
    public const string LyricsTimeline = "/api/mobile/audio/lyrics-timeline";
    public const string QrLookup = "/api/mobile/qr/lookup";
    public const string AboutSections = "/api/mobile/about/sections";
}

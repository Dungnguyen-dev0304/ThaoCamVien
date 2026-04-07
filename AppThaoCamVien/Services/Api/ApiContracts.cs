namespace AppThaoCamVien.Services.Api;

public sealed class HomeFeedDto
{
    public string Greeting { get; set; } = string.Empty;
    public List<HomeCardDto> HappenNow { get; set; } = [];
    public List<HomeCardDto> Recommended { get; set; } = [];
}

public sealed class HomeCardDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
}

public sealed class WeatherDto
{
    public string Condition { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public string Advice { get; set; } = string.Empty;
}

public sealed class PoiClusterDto
{
    public int Count { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class NearbyPoiDto
{
    public int PoiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
}

public sealed class VoiceDto
{
    public string VoiceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
}

public sealed class LyricLineDto
{
    public int StartMs { get; set; }
    public int EndMs { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class QrLookupRequest
{
    public string Code { get; set; } = string.Empty;
}

public sealed class AboutSectionDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

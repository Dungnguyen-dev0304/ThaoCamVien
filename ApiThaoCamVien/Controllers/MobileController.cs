using ApiThaoCamVien.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;

namespace ApiThaoCamVien.Controllers;

/// <summary>
/// Endpoints cho app MAUI (ApiFirstUxSpec). Trước đây chỉ có /api/Pois nên màn Home gọi
/// /api/mobile/* sẽ 404 → app báo lỗi dù backend Pois vẫn log "Found N POIs".
/// </summary>
[Route("api/mobile")]
[ApiController]
public sealed class MobileController : ControllerBase
{
    private readonly WebContext _ctx;
    private readonly ILogger<MobileController> _logger;

    public MobileController(WebContext ctx, ILogger<MobileController> logger)
    {
        _ctx = ctx;
        _logger = logger;
    }

    /// <summary>GET /api/mobile/home-feed?lang=vi</summary>
    [HttpGet("home-feed")]
    public async Task<IActionResult> GetHomeFeed([FromQuery] string lang = "vi")
    {
        var pois = await GetActivePoisOrderedAsync();
        var cards = pois.Select(p => new HomeFeedCardResponse
        {
            Id = p.PoiId,
            Title = p.Name ?? "",
            Subtitle = Truncate(p.Description, 80),
            ThumbnailUrl = string.IsNullOrWhiteSpace(p.ImageThumbnail) ? "" : p.ImageThumbnail!
        }).ToList();

        var mid = (cards.Count + 1) / 2;
        var greeting = lang switch
        {
            "en" => "Welcome to Saigon Zoo & Botanical Gardens!",
            "th" => "ยินดีต้อนรับสู่สวนสัตว์ไซง่อน!",
            _ => "Chào mừng đến Thảo Cầm Viên!"
        };

        var topImage = cards.FirstOrDefault()?.ThumbnailUrl ?? "";

        var dto = new HomeFeedResponse
        {
            Greeting = greeting,
            HeaderTitle = greeting,
            HeaderSubtitle = "Khám phá thiên nhiên hoang dã",
            ContinueListeningKicker = lang == "en" ? "CONTINUE LISTENING" : "TIẾP TỤC NGHE",
            NearbyPlacesTitle = lang == "en" ? "NEARBY PLACES" : "GẦN BẠN",
            StartTourTitle = lang == "en" ? "START TOUR" : "BẮT ĐẦU HÀNH TRÌNH",
            StartTourButtonText = lang == "en" ? "Start Tour" : "BẮT ĐẦU",
            StartTourBackgroundImageUrl = topImage,
            HappenNow = cards.Take(mid).ToList(),
            Recommended = cards.Skip(mid).ToList()
        };

        return Ok(dto);
    }

    /// <summary>GET /api/mobile/weather/current?lang=vi — dữ liệu demo local (không gọi dịch vụ thời tiết).</summary>
    [HttpGet("weather/current")]
    public Task<IActionResult> GetWeather([FromQuery] string lang = "vi")
    {
        var dto = lang switch
        {
            "en" => new WeatherResponse
            {
                Condition = "Partly cloudy",
                TemperatureC = 31,
                Advice = "Comfortable for a walk — stay hydrated."
            },
            _ => new WeatherResponse
            {
                Condition = "Nhiều mây",
                TemperatureC = 31,
                Advice = "Thời tiết thuận lợi để tham quan — nhớ mang nước."
            }
        };
        return Task.FromResult<IActionResult>(Ok(dto));
    }

    /// <summary>GET /api/mobile/pois/clusters</summary>
    [HttpGet("pois/clusters")]
    public async Task<IActionResult> GetClusters([FromQuery] int z = 14, [FromQuery] string? bbox = null)
    {
        var pois = await GetActivePoisOrderedAsync();
        var clusters = pois.Select(p => new PoiClusterResponse
        {
            Count = 1,
            Latitude = (double)p.Latitude,
            Longitude = (double)p.Longitude
        }).ToList();
        return Ok(clusters);
    }

    /// <summary>GET /api/mobile/pois/nearby</summary>
    [HttpGet("pois/nearby")]
    public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lng, [FromQuery] int radius = 200, [FromQuery] string lang = "vi")
    {
        var pois = await GetActivePoisOrderedAsync();
        var list = new List<NearbyPoiResponse>();
        foreach (var p in pois)
        {
            var d = HaversineMeters(lat, lng, (double)p.Latitude, (double)p.Longitude);
            if (d <= radius)
            {
                list.Add(new NearbyPoiResponse
                {
                    PoiId = p.PoiId,
                    Name = p.Name ?? "",
                    ThumbnailUrl = p.ImageThumbnail ?? "",
                    DistanceMeters = d,
                    DistanceLabel = $"{Math.Round(d):0}m",
                    LocationHint = d <= 60
                        ? (lang == "en" ? "Near" : "Gần")
                        : (lang == "en" ? "Nearby" : "Gần đây")
                });
            }
        }
        list = list.OrderBy(x => x.DistanceMeters).ToList();
        return Ok(list);
    }

    /// <summary>GET /api/mobile/animals?lang=vi</summary>
    [HttpGet("animals")]
    public async Task<IActionResult> GetAnimals([FromQuery] string lang = "vi")
    {
        // Animals = active POIs grouped by their POI category.
        // Conservation status is derived from Priority (no extra table in current DB schema).
        var q =
            from p in _ctx.Pois.AsNoTracking().Where(x => x.IsActive)
            join c in _ctx.PoiCategories.AsNoTracking() on p.CategoryId equals (int?)c.CategoryId into pc
            from c in pc.DefaultIfEmpty()
            select new
            {
                p.PoiId,
                p.Name,
                p.ImageThumbnail,
                p.Priority,
                CategoryName = c != null ? c.CategoryName : ""
            };

        var items = await q.OrderByDescending(x => x.Priority).ToListAsync();

        string statusVi(int? prio)
        {
            var p0 = prio ?? 1;
            if (p0 >= 9) return "Sắp nguy cấp";
            if (p0 >= 5) return "Dễ tổn thương";
            return "An toàn";
        }

        string colorHex(int? prio)
        {
            var p0 = prio ?? 1;
            if (p0 >= 9) return "#E53935";
            if (p0 >= 5) return "#FB8C00";
            return "#2E7D32";
        }

        string statusEn(int? prio)
        {
            var p0 = prio ?? 1;
            if (p0 >= 9) return "Critically endangered";
            if (p0 >= 5) return "Vulnerable";
            return "Safe";
        }

        var animals = items.Select(x => new AnimalResponse
        {
            Id = x.PoiId,
            Name = x.Name ?? "",
            ImageUrl = x.ImageThumbnail ?? "",
            Category = string.IsNullOrWhiteSpace(x.CategoryName) ? "Khác" : x.CategoryName,
            ConservationStatus = lang == "en" ? statusEn(x.Priority) : statusVi(x.Priority),
            StatusColorHex = colorHex(x.Priority)
        }).ToList();

        var filters = new List<AnimalFilterResponse>
        {
            new() { Title = lang == "en" ? "All" : "Tất cả", Category = "" }
        };
        foreach (var cat in animals.Select(a => a.Category).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s))
        {
            filters.Add(new AnimalFilterResponse { Title = cat, Category = cat });
        }

        var dto = new AnimalsResponse { Animals = animals, Filters = filters };
        return Ok(dto);
    }

    /// <summary>GET /api/mobile/tts/voices?lang=vi</summary>
    [HttpGet("tts/voices")]
    public Task<IActionResult> GetVoices([FromQuery] string lang = "vi")
    {
        var voices = new List<VoiceResponse>
        {
            new()
            {
                VoiceId = "vi-VN-Neural2-A",
                DisplayName = lang == "en" ? "Vietnamese (Neural)" : "Tiếng Việt (Neural)",
                Locale = "vi-VN",
                Gender = "female"
            },
            new()
            {
                VoiceId = "en-US-Neural2-D",
                DisplayName = "English (US)",
                Locale = "en-US",
                Gender = "male"
            }
        };
        return Task.FromResult<IActionResult>(Ok(voices));
    }

    /// <summary>GET /api/mobile/audio/lyrics-timeline?poiId=1&amp;voiceId=...</summary>
    [HttpGet("audio/lyrics-timeline")]
    public Task<IActionResult> GetLyricsTimeline([FromQuery] int poiId, [FromQuery] string? voiceId = null)
    {
        // Chưa có timeline thật trong DB — trả rỗng để app không lỗi parse.
        return Task.FromResult<IActionResult>(Ok(Array.Empty<LyricLineResponse>()));
    }

    /// <summary>POST /api/mobile/qr/lookup</summary>
    [HttpPost("qr/lookup")]
    public async Task<IActionResult> QrLookup([FromBody] QrLookupRequestBody body)
    {
        var code = body?.Code?.Trim() ?? "";
        if (string.IsNullOrEmpty(code))
            return BadRequest(new { message = "Missing code" });

        var qr = await _ctx.QrCodes.AsNoTracking().FirstOrDefaultAsync(q => q.QrCodeData == code);
        if (qr == null)
            return NotFound(new { message = "QR not found" });

        var poi = await _ctx.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.PoiId == qr.PoiId);
        if (poi == null)
            return NotFound(new { message = "POI not found" });

        var dto = new NearbyPoiResponse
        {
            PoiId = poi.PoiId,
            Name = poi.Name ?? "",
            ThumbnailUrl = poi.ImageThumbnail ?? "",
            DistanceMeters = 0
        };
        return Ok(dto);
    }

    /// <summary>GET /api/mobile/about/sections</summary>
    [HttpGet("about/sections")]
    public Task<IActionResult> GetAboutSections()
    {
        var sections = new List<AboutSectionResponse>
        {
            new()
            {
                Key = "intro",
                Title = "Thảo Cầm Viên",
                Body = "Ứng dụng thuyết minh tự động — dữ liệu đồng bộ từ máy chủ nội bộ khi phát triển."
            }
        };
        return Task.FromResult<IActionResult>(Ok(sections));
    }

    private async Task<List<Poi>> GetActivePoisOrderedAsync()
    {
        var q = _ctx.Pois.AsNoTracking().Where(p => p.IsActive == true)
            .OrderByDescending(p => p.Priority);
        var list = await q.ToListAsync();
        if (list.Count == 0)
        {
            _logger.LogWarning("Mobile: no active POIs, falling back to all POIs.");
            list = await _ctx.Pois.AsNoTracking().OrderByDescending(p => p.Priority).ToListAsync();
        }
        return list;
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

#region Response DTOs (khớp JSON với AppThaoCamVien.Services.Api DTOs)

internal sealed class HomeFeedResponse
{
    public string Greeting { get; set; } = "";
    public string HeaderTitle { get; set; } = "";
    public string HeaderSubtitle { get; set; } = "";
    public string ContinueListeningKicker { get; set; } = "";
    public string NearbyPlacesTitle { get; set; } = "";
    public string StartTourTitle { get; set; } = "";
    public string StartTourButtonText { get; set; } = "";
    public string StartTourBackgroundImageUrl { get; set; } = "";
    public List<HomeFeedCardResponse> HappenNow { get; set; } = [];
    public List<HomeFeedCardResponse> Recommended { get; set; } = [];
}

internal sealed class HomeFeedCardResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
}

internal sealed class WeatherResponse
{
    public string Condition { get; set; } = "";
    public double TemperatureC { get; set; }
    public string Advice { get; set; } = "";
}

internal sealed class PoiClusterResponse
{
    public int Count { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

internal sealed class NearbyPoiResponse
{
    public int PoiId { get; set; }
    public string Name { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public double DistanceMeters { get; set; }
    public string DistanceLabel { get; set; } = "";
    public string LocationHint { get; set; } = "";
}

internal sealed class VoiceResponse
{
    public string VoiceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Locale { get; set; } = "";
    public string Gender { get; set; } = "";
}

internal sealed class LyricLineResponse
{
    public int StartMs { get; set; }
    public int EndMs { get; set; }
    public string Text { get; set; } = "";
}

public sealed class QrLookupRequestBody
{
    public string Code { get; set; } = "";
}

internal sealed class AboutSectionResponse
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
}

internal sealed class AnimalsResponse
{
    public List<AnimalResponse> Animals { get; set; } = [];
    public List<AnimalFilterResponse> Filters { get; set; } = [];
}

internal sealed class AnimalResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string Category { get; set; } = "";
    public string ConservationStatus { get; set; } = "";
    public string StatusColorHex { get; set; } = "";
}

internal sealed class AnimalFilterResponse
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
}

#endregion

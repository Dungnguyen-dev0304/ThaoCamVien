using ApiThaoCamVien.Models;
using ApiThaoCamVien.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApiThaoCamVien;
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
    private readonly PoiLocalizationService _poiLocalization;

    public MobileController(WebContext ctx, ILogger<MobileController> logger, PoiLocalizationService poiLocalization)
    {
        _ctx = ctx;
        _logger = logger;
        _poiLocalization = poiLocalization;
    }

    /// <summary>
    /// Resolve ngôn ngữ: Accept-Language header (ưu tiên) → query param → "vi".
    /// Mobile app gửi Accept-Language header qua ApiService.ApplyLanguageHeader().
    /// Web browser / Swagger vẫn dùng ?lang= query param.
    /// </summary>
    private string ResolveLang(string? queryLang)
    {
        // Ưu tiên 1: Accept-Language header
        var acceptLang = Request.Headers.AcceptLanguage.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(acceptLang))
        {
            // Header có thể là "en", "en-US", "vi-VN,vi;q=0.9" — lấy language code đầu tiên
            var primary = acceptLang.Split(',')[0].Trim().Split('-')[0].Trim().Split(';')[0].Trim();
            if (!string.IsNullOrEmpty(primary) && primary.Length >= 2)
                return primary.ToLowerInvariant();
        }

        // Ưu tiên 2: Query param
        if (!string.IsNullOrWhiteSpace(queryLang))
            return queryLang.ToLowerInvariant();

        return "vi";
    }

    /// <summary>GET /api/mobile/home-feed?lang=vi</summary>
    [HttpGet("home-feed")]
    public async Task<IActionResult> GetHomeFeed([FromQuery] string? lang = null)
    {
        lang = ResolveLang(lang);
        var pois = await GetActivePoisOrderedAsync();
        await _poiLocalization.ApplyToPoisAsync(pois, lang, HttpContext.RequestAborted);
        var cards = pois.Select(p => new HomeFeedCardResponse
        {
            Id = p.PoiId,
            Title = p.Name ?? "",
            Subtitle = Truncate(p.Description, 80),
            ThumbnailUrl = PoiMediaUrls.ResolveThumbnail(Request, p.ImageThumbnail)
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
            HeaderSubtitle = lang switch
            {
                "en" => "Discover the amazing world of wildlife",
                "th" => "ค้นพบโลกของสัตว์ป่า",
                _ => "Khám phá thiên nhiên hoang dã"
            },
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
    public Task<IActionResult> GetWeather([FromQuery] string? lang = null)
    {
        lang = ResolveLang(lang);
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
    public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lng, [FromQuery] int radius = 200, [FromQuery] string? lang = null)
    {
        lang = ResolveLang(lang);
        var pois = await GetActivePoisOrderedAsync();
        await _poiLocalization.ApplyToPoisAsync(pois, lang, HttpContext.RequestAborted);
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
                    ThumbnailUrl = PoiMediaUrls.ResolveThumbnail(Request, p.ImageThumbnail),
                    DistanceMeters = d,
                    DistanceLabel = $"{Math.Round(d):0}m",
                    LocationHint = d <= 60
                        ? (lang == "en" ? "Near" : "Gần")
                        : (lang == "en" ? "Nearby" : "Gần đây")
                });
            }
        }
        list = list.OrderBy(x => x.DistanceMeters).ToList();

        // Không có POI trong bán kính (GPS ngoài vườn / tọa độ DB) → vẫn trả 5 điểm nổi bật cho màn Home.
        if (list.Count == 0)
        {
            var hint = lang == "en" ? "Saigon Zoo" : "Thảo Cầm Viên";
            var label = lang == "en" ? "FEATURED" : "NỔI BẬT";
            list = pois.Take(5).Select(p => new NearbyPoiResponse
            {
                PoiId = p.PoiId,
                Name = p.Name ?? "",
                ThumbnailUrl = PoiMediaUrls.ResolveThumbnail(Request, p.ImageThumbnail),
                DistanceMeters = 0,
                DistanceLabel = label,
                LocationHint = hint
            }).ToList();
        }

        return Ok(list);
    }

    /// <summary>GET /api/mobile/animals?lang=vi</summary>
    [HttpGet("animals")]
    public async Task<IActionResult> GetAnimals([FromQuery] string? lang = null)
    {
        lang = ResolveLang(lang);

        // Load POIs kèm category name
        var pois = await GetActivePoisOrderedAsync();

        await _poiLocalization.ApplyToPoisAsync(pois, lang, HttpContext.RequestAborted);

        // Load category names
        var categories = await _ctx.PoiCategories.AsNoTracking().ToListAsync();
        var catLookup = categories.ToDictionary(c => c.CategoryId, c => c.CategoryName ?? "");

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

        var defaultCat = lang == "en" ? "Other" : "Khác";
        var catTrans = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cn in catLookup.Values.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
            catTrans[cn] = await _poiLocalization.TranslatePlainAsync(cn, lang, HttpContext.RequestAborted);

        var animals = new List<AnimalResponse>();
        foreach (var p in pois)
        {
            var catVi = p.CategoryId.HasValue && catLookup.TryGetValue(p.CategoryId.Value, out var cnv) && !string.IsNullOrWhiteSpace(cnv)
                ? cnv
                : null;
            var category = string.IsNullOrEmpty(catVi) ? defaultCat : catTrans[catVi];

            animals.Add(new AnimalResponse
            {
                Id = p.PoiId,
                Name = p.Name ?? "",
                ImageUrl = PoiMediaUrls.ResolveThumbnail(Request, p.ImageThumbnail),
                Category = category,
                ConservationStatus = lang == "en" ? statusEn(p.Priority) : statusVi(p.Priority),
                StatusColorHex = colorHex(p.Priority),
                IsPremium = p.IsPremium,
                PremiumPrice = p.PremiumPrice
            });
        }

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
    public Task<IActionResult> GetVoices([FromQuery] string? lang = null)
    {
        lang = ResolveLang(lang);
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

    /// <summary>
    /// POST /api/mobile/qr/lookup
    ///
    /// Hỗ trợ nhiều format mã QR:
    ///   - "TCV-1", "TCV-001", "TCV1" → POI ID 1
    ///   - "1", "001" → POI ID 1
    ///   - Bất kỳ string khác → tìm trong bảng qr_codes
    /// </summary>
    [HttpPost("qr/lookup")]
    public async Task<IActionResult> QrLookup([FromBody] QrLookupRequestBody body)
    {
        var code = body?.Code?.Trim() ?? "";
        if (string.IsNullOrEmpty(code))
            return BadRequest(new { message = "Missing code" });

        Poi? poi = null;

        // Thử parse POI ID trực tiếp từ mã QR
        // Hỗ trợ: "TCV-1", "TCV-001", "TCV1", "1", "001"
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            code, @"^TCV[- ]?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (int.TryParse(cleaned, out int poiId) && poiId > 0)
        {
            poi = await _ctx.Pois.AsNoTracking()
                .FirstOrDefaultAsync(p => p.PoiId == poiId && p.IsActive);
        }

        // Fallback: tìm trong bảng qr_codes
        if (poi == null)
        {
            var qr = await _ctx.QrCodes.AsNoTracking()
                .FirstOrDefaultAsync(q => q.QrCodeData == code);
            if (qr != null)
            {
                poi = await _ctx.Pois.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PoiId == qr.PoiId);
            }
        }

        if (poi == null)
            return NotFound(new { message = "QR not found" });

        var lang = ResolveLang(null);
        await _poiLocalization.ApplyToPoisAsync(new List<Poi> { poi }, lang, HttpContext.RequestAborted);

        var dto = new NearbyPoiResponse
        {
            PoiId = poi.PoiId,
            Name = poi.Name ?? "",
            ThumbnailUrl = PoiMediaUrls.ResolveThumbnail(Request, poi.ImageThumbnail),
            DistanceMeters = 0
        };
        return Ok(dto);
    }

    /// <summary>
    /// POST /api/mobile/presence — heartbeat from the app when online (admin live user count).
    /// </summary>
    [HttpPost("presence")]
    public async Task<IActionResult> PostPresence([FromBody] PresencePingBody? body)
    {
        var sessionId = body?.SessionId?.Trim() ?? "";
        if (sessionId.Length is < 8 or > 64)
            return BadRequest(new { message = "Invalid sessionId" });

        var now = DateTime.UtcNow;
        var row = await _ctx.AppClientPresences.FindAsync(new object[] { sessionId }, HttpContext.RequestAborted);
        if (row == null)
        {
            _ctx.AppClientPresences.Add(new AppClientPresence
            {
                SessionId = sessionId,
                LastSeenUtc = now,
                CurrentPoiId = body?.CurrentPoiId
            });
        }
        else
        {
            row.LastSeenUtc = now;
            if (body?.CurrentPoiId is int poiId && poiId > 0)
                row.CurrentPoiId = poiId;
        }

        await _ctx.SaveChangesAsync(HttpContext.RequestAborted);
        return Ok(new PresencePingResponse { Ok = true, ServerUtc = now });
    }

    // ─── HÀNG ĐỢI FIFO khi nhiều người cùng quét 1 QR ────────────────────
    // Kịch bản: 50 học sinh đứng trước chuồng hổ cùng quét TCV-1.
    // Server gán thứ tự bằng IDENTITY → ai INSERT trước → phát audio trước.
    // Mỗi POI là 1 hàng đợi độc lập; sang POI khác thì tạo ticket mới.

    /// <summary>POST /api/mobile/queue/join — xin số thứ tự cho POI hiện tại.</summary>
    [HttpPost("queue/join")]
    public async Task<IActionResult> QueueJoin(
        [FromBody] QueueJoinBody body,
        [FromServices] QrQueueService queue)
    {
        if (body == null || body.PoiId <= 0 || string.IsNullOrWhiteSpace(body.SessionId))
            return BadRequest(new { message = "Invalid payload (poiId + sessionId required)" });
        if (body.SessionId.Length is < 8 or > 64)
            return BadRequest(new { message = "Invalid sessionId length" });

        var r = await queue.JoinAsync(body.PoiId, body.SessionId, HttpContext.RequestAborted);
        return Ok(new
        {
            ticketId = r.TicketId,
            position = r.Position,
            total = r.Total,
        });
    }

    /// <summary>GET /api/mobile/queue/status/{ticketId} — poll xem đã đến lượt chưa.</summary>
    [HttpGet("queue/status/{ticketId:long}")]
    public async Task<IActionResult> QueueStatus(
        long ticketId,
        [FromServices] QrQueueService queue)
    {
        var s = await queue.GetStatusAsync(ticketId, HttpContext.RequestAborted);
        if (s == null) return NotFound(new { message = "Ticket expired or finished" });

        return Ok(new
        {
            ticketId = s.TicketId,
            position = s.Position,
            total = s.Total,
            isPlaying = s.IsPlaying,
        });
    }

    /// <summary>DELETE /api/mobile/queue/{ticketId} — báo server "đã phát xong".</summary>
    [HttpDelete("queue/{ticketId:long}")]
    public async Task<IActionResult> QueueLeave(
        long ticketId,
        [FromServices] QrQueueService queue)
    {
        var ok = await queue.LeaveAsync(ticketId, HttpContext.RequestAborted);
        if (!ok) return NotFound(new { message = "Ticket not found" });
        return Ok(new { ok = true });
    }

    /// <summary>GET /api/mobile/about/sections</summary>
    [HttpGet("about/sections")]
    public Task<IActionResult> GetAboutSections([FromQuery] string? lang = null)
    {
        lang = ResolveLang(lang);
        var sections = new List<AboutSectionResponse>
        {
            new()
            {
                Key = "intro",
                Title = lang == "en" ? "Saigon Zoo & Botanical Gardens" : "Thảo Cầm Viên",
                Body = lang == "en"
                    ? "Automatic narration app — content syncs from your server when online."
                    : "Ứng dụng thuyết minh tự động — dữ liệu đồng bộ từ máy chủ nội bộ khi phát triển."
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

public sealed class PresencePingBody
{
    public string SessionId { get; set; } = "";
    public int? CurrentPoiId { get; set; }
}

public sealed class QueueJoinBody
{
    public int PoiId { get; set; }
    public string SessionId { get; set; } = "";
}

public sealed class PresencePingResponse
{
    public bool Ok { get; set; }
    public DateTime ServerUtc { get; set; }
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

    /// <summary>true = POI bị khóa, cần thanh toán Premium MoMo mới nghe được.</summary>
    public bool IsPremium { get; set; }

    /// <summary>Giá Premium (VND). NULL nếu chưa cấu hình giá.</summary>
    public decimal? PremiumPrice { get; set; }
}

internal sealed class AnimalFilterResponse
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
}

#endregion

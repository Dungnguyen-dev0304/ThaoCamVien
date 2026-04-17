using System.Security.Cryptography;
using System.Text;
using ApiThaoCamVien.Models;
using GTranslate.Translators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SharedThaoCamVien.Models;

namespace ApiThaoCamVien.Services;

/// <summary>
/// Ưu tiên bản dịch thủ công trong <c>poi_translations</c>; nếu thiếu thì dịch tự động (GTranslate)
/// từ nội dung tiếng Việt gốc trong bảng <c>pois</c> sang ngôn ngữ app (vd. en).
/// </summary>
public sealed class PoiLocalizationService
{
    private readonly WebContext _ctx;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PoiLocalizationService> _logger;
    private readonly AggregateTranslator _translator = new();

    public PoiLocalizationService(WebContext ctx, IMemoryCache cache, ILogger<PoiLocalizationService> logger)
    {
        _ctx = ctx;
        _cache = cache;
        _logger = logger;
    }

    private static string CacheKey(string lang, string text)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lang + "|" + text)))[..32];
        return $"poi_tr:{lang}:{hash}";
    }

    /// <summary>Dịch một đoạn văn bản tùy ý (tên danh mục, mô tả tour, …).</summary>
    public async Task<string> TranslatePlainAsync(string? text, string lang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return text ?? "";
        lang = (lang ?? "vi").ToLowerInvariant();
        if (lang == "vi") return text;

        var key = CacheKey(lang, text);
        if (_cache.TryGetValue(key, out string? cached) && cached != null)
            return cached;

        try
        {
            var result = await _translator.TranslateAsync(text, lang);
            var translated = string.IsNullOrWhiteSpace(result.Translation) ? text : result.Translation;
            _cache.Set(key, translated, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });
            return translated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TranslatePlain failed lang={Lang} len={Len}", lang, text.Length);
            return text;
        }
    }

    /// <summary>Áp dụng bản dịch DB + tự động lấp chỗ trống cho Name/Description của từng POI.</summary>
    public async Task ApplyToPoisAsync(List<Poi> pois, string lang, CancellationToken ct = default)
    {
        if (pois == null || pois.Count == 0) return;
        lang = (lang ?? "vi").ToLowerInvariant();
        if (lang == "vi") return;

        var snapshot = pois.ToDictionary(p => p.PoiId, p => (Name: p.Name ?? "", Desc: p.Description ?? ""));

        try
        {
            var poiIds = pois.Select(p => p.PoiId).ToList();
            var rows = await _ctx.PoiTranslations
                .AsNoTracking()
                .Where(t => t.LanguageCode == lang && poiIds.Contains(t.PoiId))
                .ToListAsync(ct);

            var lookup = rows
                .GroupBy(t => t.PoiId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var poi in pois)
            {
                if (!lookup.TryGetValue(poi.PoiId, out var tr)) continue;
                if (!string.IsNullOrWhiteSpace(tr.Name)) poi.Name = tr.Name;
                if (!string.IsNullOrWhiteSpace(tr.Description)) poi.Description = tr.Description;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PoiTranslations lookup failed lang={Lang}", lang);
        }

        foreach (var poi in pois)
        {
            if (!snapshot.TryGetValue(poi.PoiId, out var snap)) continue;

            var curName = (poi.Name ?? "").Trim();
            var snapName = snap.Name.Trim();
            if (string.Equals(curName, snapName, StringComparison.Ordinal) && snapName.Length > 0)
                poi.Name = await TranslatePlainAsync(snap.Name, lang, ct);

            var curDesc = (poi.Description ?? "").Trim();
            var snapDesc = snap.Desc.Trim();
            if (string.Equals(curDesc, snapDesc, StringComparison.Ordinal) && snapDesc.Length > 0)
                poi.Description = await TranslatePlainAsync(snap.Desc, lang, ct);
        }
    }
}

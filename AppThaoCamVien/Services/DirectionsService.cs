using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using NetTopologySuite.Geometries;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

#pragma warning disable CS8604 // Possible null reference argument — Mapsui API nullable annotations

namespace AppThaoCamVien.Services;

/// <summary>
/// Google Directions API client + polyline decoder cho Mapsui.
///
/// Flow:
///   1. Gọi Google Directions API (walking mode — phù hợp trong zoo)
///   2. Decode encoded polyline → List of (lat, lng)
///   3. Build Mapsui MemoryLayer chứa LineString
///
/// Nếu chưa có API Key → trả line thẳng (straight-line fallback).
/// API Key được lưu trong Preferences ("GoogleDirectionsApiKey").
/// </summary>
public sealed class DirectionsService
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline _pipeline;

    private const string DirectionsBaseUrl =
        "https://maps.googleapis.com/maps/api/directions/json";

    /// <summary>Tên layer route trên MapView — dùng để xoá/replace.</summary>
    public const string RouteLayerName = "RoutePolylineLayer";

    public DirectionsService()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _pipeline = ResiliencePolicies.HttpPipeline;
    }

    /// <summary>API Key từ Preferences. Trả empty nếu chưa cấu hình.</summary>
    public static string ApiKey
    {
        get => Preferences.Default.Get("GoogleDirectionsApiKey", string.Empty);
        set => Preferences.Default.Set("GoogleDirectionsApiKey", value);
    }

    public static bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

   
    public async Task<RouteResult> GetRouteAsync(
        double originLat, double originLng,
        double destLat, double destLng,
        CancellationToken ct = default)
    {
        if (!HasApiKey)
        {
            Debug.WriteLine("[Directions] No API key — using straight-line fallback.");
            return new RouteResult
            {
                Points = [(originLat, originLng), (destLat, destLng)],
                DurationText = "—",
                DistanceText = $"{HaversineMeters(originLat, originLng, destLat, destLng):F0}m (đường chim bay)",
                IsFallback = true
            };
        }

        try
        {
            return await _pipeline.ExecuteAsync(async token =>
            {
                var url = $"{DirectionsBaseUrl}" +
                          $"?origin={originLat},{originLng}" +
                          $"&destination={destLat},{destLng}" +
                          $"&mode=walking" +
                          $"&key={ApiKey}";

                var response = await _httpClient.GetFromJsonAsync<DirectionsApiResponse>(url, token);

                if (response?.Routes == null || response.Routes.Count == 0)
                {
                    Debug.WriteLine($"[Directions] API returned no routes. Status={response?.Status}");
                    return StraightLineFallback(originLat, originLng, destLat, destLng);
                }

                var route = response.Routes[0];
                var points = new List<(double Lat, double Lng)>();

                // Decode từng step → nối thành 1 polyline liên tục
                foreach (var leg in route.Legs)
                {
                    foreach (var step in leg.Steps)
                    {
                        var decoded = DecodePolyline(step.Polyline.Points);
                        points.AddRange(decoded);
                    }
                }

                // Loại bỏ duplicate liên tiếp (điểm cuối step N = điểm đầu step N+1)
                var cleaned = new List<(double Lat, double Lng)>();
                foreach (var p in points)
                {
                    if (cleaned.Count == 0 ||
                        Math.Abs(cleaned[^1].Lat - p.Lat) > 1e-8 ||
                        Math.Abs(cleaned[^1].Lng - p.Lng) > 1e-8)
                    {
                        cleaned.Add(p);
                    }
                }

                var leg0 = route.Legs[0];
                return new RouteResult
                {
                    Points = cleaned,
                    DurationText = leg0.Duration.Text,
                    DistanceText = leg0.Distance.Text,
                    IsFallback = false
                };
            }, ct);
        }
        catch (BrokenCircuitException)
        {
            Debug.WriteLine("[Directions] Circuit open — fallback.");
            return StraightLineFallback(originLat, originLng, destLat, destLng);
        }
        catch (TimeoutRejectedException)
        {
            Debug.WriteLine("[Directions] Timeout — fallback.");
            return StraightLineFallback(originLat, originLng, destLat, destLng);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Directions] Error: {ex.Message} — fallback.");
            return StraightLineFallback(originLat, originLng, destLat, destLng);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC: Build Mapsui layer
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo MemoryLayer chứa LineString từ route points.
    /// Dùng SphericalMercator projection (khớp với OpenStreetMap tiles).
    /// </summary>
    public static MemoryLayer BuildRouteLayer(List<(double Lat, double Lng)> points)
    {
        if (points.Count < 2)
            return new MemoryLayer { Name = RouteLayerName };

        // Chuyển (lat, lng) → Mapsui SphericalMercator
        var coords = points.Select(p =>
        {
            var (x, y) = SphericalMercator.FromLonLat(p.Lng, p.Lat);
            return new Coordinate(x, y);
        }).ToArray();

        var lineString = new LineString(coords);
        var feature = new GeometryFeature(lineString);

        var routeStyle = new VectorStyle
        {
            Line = new Pen(new Mapsui.Styles.Color(27, 94, 58), 5) // Xanh lá đậm (#1B5E3A), 5px
        };

        feature.Styles.Add(routeStyle);

        var features = new List<IFeature> { feature };
        return new MemoryLayer
        {
            Name = RouteLayerName,
            Features = features,
            Style = null // Feature-level style
        };
    }

    /// <summary>Xóa route layer cũ khỏi Map (nếu có).</summary>
    public static void RemoveRouteLayer(Mapsui.Map? map)
    {
        if (map == null) return;
        try
        {
            // Dùng LINQ thay vì FindLayer (API có thể khác giữa Mapsui versions)
            var toRemove = map.Layers
                .Where(l => l.Name == RouteLayerName)
                .ToList();

            foreach (var layer in toRemove)
                map.Layers.Remove(layer);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Directions] RemoveRouteLayer: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POLYLINE DECODER (Google Encoded Polyline Algorithm)
    // Ref: https://developers.google.com/maps/documentation/utilities/polylinealgorithm
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Decode Google's encoded polyline string → list of (lat, lng).
    /// Algorithm: variable-length encoding, 5-bit chunks, offset-encoded.
    /// </summary>
    public static List<(double Lat, double Lng)> DecodePolyline(string encoded)
    {
        var result = new List<(double Lat, double Lng)>();
        if (string.IsNullOrEmpty(encoded)) return result;

        int index = 0, lat = 0, lng = 0;
        int len = encoded.Length;

        while (index < len)
        {
            // Decode latitude
            int shift = 0, value = 0;
            int chunk;
            do
            {
                chunk = encoded[index++] - 63;
                value |= (chunk & 0x1F) << shift;
                shift += 5;
            } while (chunk >= 0x20 && index < len);

            lat += (value & 1) != 0 ? ~(value >> 1) : (value >> 1);

            // Decode longitude
            shift = 0; value = 0;
            do
            {
                chunk = encoded[index++] - 63;
                value |= (chunk & 0x1F) << shift;
                shift += 5;
            } while (chunk >= 0x20 && index < len);

            lng += (value & 1) != 0 ? ~(value >> 1) : (value >> 1);

            result.Add((lat / 1e5, lng / 1e5));
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static RouteResult StraightLineFallback(
        double oLat, double oLng, double dLat, double dLng)
    {
        return new RouteResult
        {
            Points = [(oLat, oLng), (dLat, dLng)],
            DurationText = "—",
            DistanceText = $"{HaversineMeters(oLat, oLng, dLat, dLng):F0}m (đường chim bay)",
            IsFallback = true
        };
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

// ═══════════════════════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Kết quả route: danh sách điểm + thông tin thời gian/khoảng cách.</summary>
public sealed class RouteResult
{
    public List<(double Lat, double Lng)> Points { get; set; } = [];
    public string DurationText { get; set; } = "";
    public string DistanceText { get; set; } = "";

    /// <summary>true nếu dùng straight-line (chưa có API Key hoặc API fail).</summary>
    public bool IsFallback { get; set; }
}

// ── Google Directions API response DTOs ─────────────────────────────────
// Chỉ map các field cần thiết, bỏ qua phần không dùng.

internal sealed class DirectionsApiResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("routes")]
    public List<DirectionsRoute> Routes { get; set; } = [];
}

internal sealed class DirectionsRoute
{
    [JsonPropertyName("legs")]
    public List<DirectionsLeg> Legs { get; set; } = [];

    [JsonPropertyName("overview_polyline")]
    public DirectionsPolyline OverviewPolyline { get; set; } = new();
}

internal sealed class DirectionsLeg
{
    [JsonPropertyName("steps")]
    public List<DirectionsStep> Steps { get; set; } = [];

    [JsonPropertyName("duration")]
    public DirectionsTextValue Duration { get; set; } = new();

    [JsonPropertyName("distance")]
    public DirectionsTextValue Distance { get; set; } = new();
}

internal sealed class DirectionsStep
{
    [JsonPropertyName("polyline")]
    public DirectionsPolyline Polyline { get; set; } = new();
}

internal sealed class DirectionsPolyline
{
    [JsonPropertyName("points")]
    public string Points { get; set; } = "";
}

internal sealed class DirectionsTextValue
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("value")]
    public int Value { get; set; }
}

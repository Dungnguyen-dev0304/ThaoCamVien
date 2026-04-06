using Microsoft.Maui.Devices.Sensors;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// Geofencing Engine — Giai đoạn 3 theo đề tài.
    /// 
    /// Haversine → tính khoảng cách chính xác.
    /// Ưu tiên: Priority cao → khoảng cách gần.
    /// Debounce 3s + Cooldown 5 phút (chống spam GPS).
    /// </summary>
    public class GeofencingEngine
    {
        private const int DEFAULT_RADIUS = 30;
        private const int DEBOUNCE_SEC = 3;
        private const double APPROACH_RATIO = 1.5; // Vùng tiếp cận = 1.5× bán kính

        private readonly Dictionary<int, DateTime> _lastTriggered = new();
        private readonly Dictionary<int, DateTime> _entryDebounce = new();
        private readonly HashSet<int> _insidePois = new();
        private List<Poi> _pois = [];

        public void SetPois(List<Poi> pois) => _pois = pois;

        /// <summary>Xử lý vị trí mới — trả về kết quả geofencing.</summary>
        public GeofenceResult Process(Location loc)
        {
            var result = new GeofenceResult();
            var now = DateTime.Now;

            foreach (var poi in _pois)
            {
                var r = poi.Radius ?? DEFAULT_RADIUS;
                var d = Haversine((double)poi.Latitude, (double)poi.Longitude,
                                  loc.Latitude, loc.Longitude);

                if (d <= r)
                {
                    result.NearbyPois.Add((poi, d));

                    if (!_insidePois.Contains(poi.PoiId))
                    {
                        if (!_entryDebounce.ContainsKey(poi.PoiId))
                            _entryDebounce[poi.PoiId] = now;
                        else if ((now - _entryDebounce[poi.PoiId]).TotalSeconds >= DEBOUNCE_SEC)
                        {
                            _insidePois.Add(poi.PoiId);
                            _entryDebounce.Remove(poi.PoiId);
                        }
                    }
                }
                else if (d <= r * APPROACH_RATIO)
                {
                    result.ApproachingPois.Add((poi, d));
                    _entryDebounce.Remove(poi.PoiId);
                }
                else
                {
                    _insidePois.Remove(poi.PoiId);
                    _entryDebounce.Remove(poi.PoiId);
                }
            }

            // Chọn POI tốt nhất: Priority cao → khoảng cách gần
            if (result.NearbyPois.Count > 0)
            {
                var best = result.NearbyPois
                    .OrderByDescending(x => x.poi.Priority ?? 0)
                    .ThenBy(x => x.dist)
                    .First();

                result.ActivePoi = best.poi;
                result.ActiveDist = best.dist;
                result.CanTrigger = CanTrigger(best.poi.PoiId, now);
            }

            return result;
        }

        public bool IsWithinRadius(Location loc, Poi poi)
            => Haversine((double)poi.Latitude, (double)poi.Longitude,
                         loc.Latitude, loc.Longitude) <= (poi.Radius ?? DEFAULT_RADIUS);

        public bool CanTrigger(int poiId, DateTime? at = null)
        {
            var now = at ?? DateTime.Now;
            return !_lastTriggered.ContainsKey(poiId)
                || (now - _lastTriggered[poiId]).TotalMinutes >= 5;
        }

        public void MarkTriggered(int poiId) => _lastTriggered[poiId] = DateTime.Now;
        public void ResetCooldown(int poiId) => _lastTriggered.Remove(poiId);

        public static double Haversine(double lat1, double lon1, double lat2, double lon2)
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

    public class GeofenceResult
    {
        public Poi? ActivePoi { get; set; }
        public double ActiveDist { get; set; }
        public bool CanTrigger { get; set; }
        public List<(Poi poi, double dist)> NearbyPois { get; set; } = [];
        public List<(Poi poi, double dist)> ApproachingPois { get; set; } = [];
    }
}

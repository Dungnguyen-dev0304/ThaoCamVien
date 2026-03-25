using Microsoft.Maui.Devices.Sensors;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    public class GeofencingEngine
    {
        private const int DEFAULT_RADIUS_METERS = 30;

        /// <summary>
        /// Kiểm tra người dùng có đang đứng trong vùng bán kính của POI không.
        /// Dùng công thức Haversine để tính khoảng cách chính xác.
        /// </summary>
        public bool IsWithinRadius(Location userLocation, Poi poi)
        {
            var radiusMeters = poi.Radius ?? DEFAULT_RADIUS_METERS;
            var distance = CalculateDistance(
                (double)poi.Latitude, (double)poi.Longitude,
                userLocation.Latitude, userLocation.Longitude);

            return distance <= radiusMeters;
        }

        /// <summary>
        /// Tính khoảng cách (mét) giữa 2 tọa độ theo công thức Haversine.
        /// </summary>
        public double CalculateDistance(
            double lat1, double lon1,
            double lat2, double lon2)
        {
            const double R = 6371000; // Bán kính Trái Đất (mét)
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180;
    }
}

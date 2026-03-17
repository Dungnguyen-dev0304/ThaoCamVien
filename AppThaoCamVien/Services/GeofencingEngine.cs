using Microsoft.Maui.Devices.Sensors;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Services
{
    public class GeofencingEngine
    {
        // Hàm kiểm tra xem user có đang đứng trong bán kính của PoI không
        public bool IsWithinRadius(Location userLocation, Poi poi)
        {
            // Ép kiểu từ decimal (database) sang double (GPS)
            Location poiLocation = new Location((double)poi.Latitude, (double)poi.Longitude);

            // Tính khoảng cách (kết quả trả về là Kilomet)
            double distanceInKm = Location.CalculateDistance(userLocation, poiLocation, DistanceUnits.Kilometers);

            // Đổi ra Mét
            double distanceInMeters = distanceInKm * 1000;

            // Bán kính kích hoạt (Mặc định 15m nếu database chưa có)
            double radius = poi.Radius ?? 15.0;

            // Trả về true nếu khoảng cách <= bán kính
            return distanceInMeters <= radius;
        }
    }
}
using Microsoft.Maui.Devices.Sensors;

namespace AppThaoCamVien.Services
{
    public class LocationService
    {
        // Sự kiện bắn ra mỗi khi người dùng bước đi và có tọa độ mới
        public event EventHandler<Location> LocationUpdated;

        // Xin quyền GPS
        public async Task<bool> CheckAndRequestLocationPermission()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }
            return status == PermissionStatus.Granted;
        }

        // Bắt đầu theo dõi
        public async void StartTracking()
        {
            try
            {
                Geolocation.LocationChanged += Geolocation_LocationChanged;

                // Lấy vị trí với độ chính xác cao nhất (High), cập nhật mỗi 2 giây
                var request = new GeolocationListeningRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(2));
                await Geolocation.StartListeningForegroundAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi bật GPS: {ex.Message}");
            }
        }

        // Dừng theo dõi (để tiết kiệm pin khi tắt app)
        public void StopTracking()
        {
            Geolocation.LocationChanged -= Geolocation_LocationChanged;
            Geolocation.StopListeningForeground();
        }

        private void Geolocation_LocationChanged(object sender, GeolocationLocationChangedEventArgs e)
        {
            // Truyền tọa độ mới lên cho MapPage
            LocationUpdated?.Invoke(this, e.Location);
        }
    }
}
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
                var request = new GeolocationListeningRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(2));
                await Geolocation.StartListeningForegroundAsync(request);

#if ANDROID
                // Kích hoạt tiến trình chạy ngầm trên Android
                var context = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(context, typeof(AppThaoCamVien.Platforms.Android.AndroidLocationService));
                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                {
                    context.StartForegroundService(intent);
                }
                else
                {
                    context.StartService(intent);
                }
#endif
            }
            catch (Exception ex) { Console.WriteLine($"Lỗi bật GPS: {ex.Message}"); }
        }

        public void StopTracking()
        {
            Geolocation.LocationChanged -= Geolocation_LocationChanged;
            Geolocation.StopListeningForeground();

#if ANDROID
            // Tắt tiến trình chạy ngầm
            var context = global::Android.App.Application.Context;
            var intent = new global::Android.Content.Intent(context, typeof(AppThaoCamVien.Platforms.Android.AndroidLocationService));
            context.StopService(intent);
#endif
        }

        private void Geolocation_LocationChanged(object sender, GeolocationLocationChangedEventArgs e)
        {
            // Truyền tọa độ mới lên cho MapPage
            LocationUpdated?.Invoke(this, e.Location);
        }
    }
}
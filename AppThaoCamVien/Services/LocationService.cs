using Microsoft.Maui.Devices.Sensors;

namespace AppThaoCamVien.Services
{
    /// <summary>
    /// Location Layer — Giai đoạn 2 theo đề tài.
    /// Android: Foreground Service (notification bar).
    /// iOS: CLLocationManager AllowsBackgroundLocationUpdates.
    /// Tối ưu pin: chỉ update khi di chuyển > 5m.
    /// </summary>
    public class LocationService : IDisposable
    {
        public event EventHandler<Location>? LocationUpdated;
        public event EventHandler<string>? StatusChanged;

        private bool _isTracking;
        private Location? _lastLoc;
        private const double MIN_MOVE_METERS = 5.0;
        private const int INTERVAL_SEC = 3;

        public bool IsTracking => _isTracking;
        public Location? LastLocation => _lastLoc;

#if IOS
        private AppThaoCamVien.Platforms.iOS.LocationBackgroundService? _iosSvc;
#endif

        public async Task<bool> CheckAndRequestPermissionAsync()
        {
            var s = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (s != PermissionStatus.Granted)
                s = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (s != PermissionStatus.Granted) return false;

#if ANDROID
            try
            {
                var bg = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
                if (bg != PermissionStatus.Granted)
                    await Permissions.RequestAsync<Permissions.LocationAlways>();
            }
            catch { }
#endif
            return true;
        }

        public async Task StartAsync()
        {
            if (_isTracking) return;
            _isTracking = true;
            StatusChanged?.Invoke(this, "Đang kết nối GPS...");

#if ANDROID
            StartAndroidFg();
            await StartMauiAsync();
#elif IOS
            StartiOS();
#else
            await StartMauiAsync();
#endif
        }

        public void Stop()
        {
            if (!_isTracking) return;
            _isTracking = false;

#if ANDROID
            StopMaui();
            StopAndroidFg();
#elif IOS
            StopiOS();
#else
            StopMaui();
#endif
            StatusChanged?.Invoke(this, "GPS đã tắt");
        }

        private async Task StartMauiAsync()
        {
            try
            {
                var req = new GeolocationListeningRequest(GeolocationAccuracy.High,
                    TimeSpan.FromSeconds(INTERVAL_SEC));
                Geolocation.LocationChanged += OnMauiLoc;
                await Geolocation.StartListeningForegroundAsync(req);
                StatusChanged?.Invoke(this, "GPS đang hoạt động");
            }
            catch (Exception ex)
            {
                _isTracking = false;
                StatusChanged?.Invoke(this, $"GPS lỗi: {ex.Message}");
            }
        }

        private void StopMaui()
        {
            Geolocation.LocationChanged -= OnMauiLoc;
            Geolocation.StopListeningForeground();
        }

        private void OnMauiLoc(object? s, GeolocationLocationChangedEventArgs e)
            => Handle(e.Location);

#if IOS
        private void StartiOS()
        {
            _iosSvc = new AppThaoCamVien.Platforms.iOS.LocationBackgroundService();
            _iosSvc.StatusChanged += (_, st) => StatusChanged?.Invoke(this, st);
            _iosSvc.LocationUpdated += (_, cl) =>
            {
                Handle(new Location(cl.Coordinate.Latitude, cl.Coordinate.Longitude)
                {
                    Accuracy = cl.HorizontalAccuracy, Timestamp = DateTimeOffset.Now
                });
            };
            _iosSvc.StartTracking();
        }
        private void StopiOS() { _iosSvc?.StopTracking(); _iosSvc = null; }
#endif

#if ANDROID
        private void StartAndroidFg()
        {
            try
            {
                var i = new Android.Content.Intent(Android.App.Application.Context,
                    typeof(AppThaoCamVien.Platforms.Android.LocationForegroundService));
                i.SetAction("START");
                Android.App.Application.Context.StartForegroundService(i);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[GPS] FG err: {ex.Message}"); }
        }
        private void StopAndroidFg()
        {
            try
            {
                var i = new Android.Content.Intent(Android.App.Application.Context,
                    typeof(AppThaoCamVien.Platforms.Android.LocationForegroundService));
                i.SetAction("STOP");
                Android.App.Application.Context.StopService(i);
            }
            catch { }
        }
#endif

        private void Handle(Location? loc)
        {
            if (loc == null) return;
            if (_lastLoc != null)
            {
                var d = GeofencingEngine.Haversine(_lastLoc.Latitude, _lastLoc.Longitude,
                                                   loc.Latitude, loc.Longitude);
                if (d < MIN_MOVE_METERS) return; // Lọc noise
            }
            _lastLoc = loc;
            StatusChanged?.Invoke(this, $"GPS ±{loc.Accuracy:F0}m");
            LocationUpdated?.Invoke(this, loc);
        }

        public async Task<Location?> GetCurrentAsync()
        {
            try
            {
                return await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));
            }
            catch { return _lastLoc; }
        }

        public void Dispose() => Stop();
    }
}

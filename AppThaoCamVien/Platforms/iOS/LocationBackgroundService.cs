// Platforms/iOS/LocationBackgroundService.cs
#if IOS
using CoreLocation;
using Foundation;

namespace AppThaoCamVien.Platforms.iOS
{
    /// <summary>
    /// iOS Background Location — dùng native CLLocationManager.
    /// Yêu cầu Info.plist:
    ///   NSLocationAlwaysAndWhenInUseUsageDescription
    ///   UIBackgroundModes → location
    /// </summary>
    public class LocationBackgroundService : NSObject, ICLLocationManagerDelegate
    {
        private CLLocationManager? _mgr;

        public event EventHandler<CLLocation>? LocationUpdated;
        public event EventHandler<string>? StatusChanged;

        public void StartTracking()
        {
            _mgr = new CLLocationManager
            {
                DesiredAccuracy = CLLocation.AccuracyBest,
                DistanceFilter = 5.0,                        // Lọc noise < 5m
                AllowsBackgroundLocationUpdates = true,
                PausesLocationUpdatesAutomatically = false,
                ShowsBackgroundLocationIndicator = true             // Chỉ báo xanh iOS
            };
            _mgr.Delegate = this;

            var status = CLLocationManager.Status;
            if (status == CLAuthorizationStatus.NotDetermined)
                _mgr.RequestAlwaysAuthorization();
            else if (status == CLAuthorizationStatus.AuthorizedWhenInUse)
                _mgr.RequestAlwaysAuthorization();

            _mgr.StartUpdatingLocation();
            StatusChanged?.Invoke(this, "GPS đang hoạt động (iOS)");
        }

        public void StopTracking()
        {
            _mgr?.StopUpdatingLocation();
            _mgr = null;
            StatusChanged?.Invoke(this, "GPS đã tắt");
        }

        [Export("locationManager:didUpdateLocations:")]
        public void LocationsUpdated(CLLocationManager manager, CLLocation[] locations)
        {
            var latest = locations.LastOrDefault();
            if (latest != null) LocationUpdated?.Invoke(this, latest);
        }

        [Export("locationManager:didFailWithError:")]
        public void Failed(CLLocationManager manager, NSError error)
            => StatusChanged?.Invoke(this, $"GPS lỗi: {error.LocalizedDescription}");

        [Export("locationManager:didChangeAuthorizationStatus:")]
        public void AuthorizationChanged(CLLocationManager manager, CLAuthorizationStatus status)
        {
            if (status == CLAuthorizationStatus.AuthorizedAlways ||
                status == CLAuthorizationStatus.AuthorizedWhenInUse)
                manager.StartUpdatingLocation();
        }
    }
}
#endif

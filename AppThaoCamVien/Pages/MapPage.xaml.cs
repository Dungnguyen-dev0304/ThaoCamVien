using Mapsui.Tiling;
using Mapsui.UI.Maui;
using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;
using Microsoft.Maui.Devices.Sensors;

namespace AppThaoCamVien.Pages;

public partial class MapPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly LocationService _locationService;
    private readonly GeofencingEngine _geofencingEngine;

    private List<Poi> _allPois = new List<Poi>();

    // Biến chống Spam (Debounce): Lưu lại điểm PoI người dùng đang đứng
    // Để không bị báo "Bạn đã đến..." liên tục mỗi 2 giây
    private Poi _currentActivePoi = null;

    public MapPage(DatabaseService databaseService, LocationService locationService, GeofencingEngine geofencingEngine)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _locationService = locationService;
        _geofencingEngine = geofencingEngine;

        SetupMap();
    }

    private void SetupMap()
    {
        ZooMap.Map?.Layers.Add(OpenStreetMap.CreateTileLayer());
        ZooMap.Map?.Widgets.Clear(); // Tắt các dòng chữ rác thống kê FPS

        // Bật lớp hiển thị vị trí người dùng (Chấm xanh)
        ZooMap.MyLocationEnabled = true;

        var centerLocation = new Position(10.7870, 106.7055);
        var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(centerLocation.Longitude, centerLocation.Latitude);
        ZooMap.Map?.Navigator?.CenterOnAndZoomTo(new Mapsui.MPoint(x, y), 2);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Nạp dữ liệu PoI từ SQLite
        await _databaseService.SeedDataAsync();
        _allPois = await _databaseService.GetAllPoisAsync();

        ZooMap.Pins.Clear();
        foreach (var poi in _allPois)
        {
            var pin = new Pin(ZooMap)
            {
                Label = poi.Name,
                Position = new Position((double)poi.Latitude, (double)poi.Longitude),
                Type = PinType.Pin,
                Color = Colors.Red
            };
            ZooMap.Pins.Add(pin);
        }

        // 2. Xin quyền và bật GPS
        bool hasPermission = await _locationService.CheckAndRequestLocationPermission();
        if (hasPermission)
        {
            // Lắng nghe sự kiện khi tọa độ thay đổi
            _locationService.LocationUpdated += OnLocationUpdated;
            _locationService.StartTracking();
        }
        else
        {
            await DisplayAlert("Quyền truy cập", "Ứng dụng cần GPS để hướng dẫn bạn tham quan.", "OK");
        }
    }

    // Khi thoát trang bản đồ thì tắt GPS để đỡ hao pin
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _locationService.LocationUpdated -= OnLocationUpdated;
        _locationService.StopTracking();
    }

    // Hàm này tự động chạy mỗi 2 giây khi có tọa độ mới từ GPS
    private void OnLocationUpdated(object sender, Location userLocation)
    {
        // UI phải được cập nhật trên luồng chính (MainThread)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // 1. Cập nhật vị trí "Chấm xanh" trên bản đồ
            ZooMap.MyLocationLayer.UpdateMyLocation(new Position(userLocation.Latitude, userLocation.Longitude));

            // 2. Quét xem có đang đứng gần thú không (Geofencing)
            bool isNearAnyPoi = false;

            foreach (var poi in _allPois)
            {
                if (_geofencingEngine.IsWithinRadius(userLocation, poi))
                {
                    isNearAnyPoi = true;

                    // Nếu đây là điểm mới (khác với điểm đang đứng lúc nãy) -> Phát thông báo
                    if (_currentActivePoi?.PoiId != poi.PoiId)
                    {
                        _currentActivePoi = poi;

                        // Ở giai đoạn 3, ta sẽ gọi hàm phát Audio tại đây. Tạm thời dùng Alert.
                        DisplayAlert("Phát hiện", $"Bạn đã bước vào khu vực: {poi.Name}!\nChuẩn bị phát thuyết minh...", "Nghe");
                    }
                    break; // Ưu tiên điểm quét trúng đầu tiên
                }
            }

            // Nếu đi ra khỏi vùng phủ sóng của tất cả các điểm, reset biến lưu trữ
            if (!isNearAnyPoi)
            {
                _currentActivePoi = null;
            }
        });
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
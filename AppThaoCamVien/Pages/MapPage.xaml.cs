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
    private readonly AudioService _audioService;

    private List<Poi> _allPois = new();
    private Poi? _currentActivePoi = null;

    public MapPage(
        DatabaseService databaseService,
        LocationService locationService,
        GeofencingEngine geofencingEngine,
        AudioService audioService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _locationService = locationService;
        _geofencingEngine = geofencingEngine;
        _audioService = audioService;

        SetupMap();
    }

    private void SetupMap()
    {
        ZooMap.Map?.Layers.Add(OpenStreetMap.CreateTileLayer());
        ZooMap.Map?.Widgets.Clear();
        ZooMap.MyLocationEnabled = true;

        var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(106.7055, 10.7870);
        ZooMap.Map?.Navigator?.CenterOnAndZoomTo(new Mapsui.MPoint(x, y), 2);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await LoadPoisAsync();
            await StartGpsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể tải dữ liệu: {ex.Message}", "OK");
        }
    }

    private async Task LoadPoisAsync()
    {
        await _databaseService.SeedDataAsync();
        // Chỉ lấy POI đang active (IsActive = true)
        _allPois = await _databaseService.GetAllPoisAsync();

        ZooMap.Pins.Clear();
        foreach (var poi in _allPois)
        {
            ZooMap.Pins.Add(new Pin(ZooMap)
            {
                Label = poi.Name,
                Position = new Position((double)poi.Latitude, (double)poi.Longitude),
                Type = PinType.Pin,
                Color = Colors.Red
            });
        }
    }

    private async Task StartGpsAsync()
    {
        bool hasPermission = await _locationService.CheckAndRequestLocationPermission();
        if (hasPermission)
        {
            _locationService.LocationUpdated += OnLocationUpdated;
            _locationService.StartTracking();
        }
        else
        {
            await DisplayAlert("Quyền truy cập",
                "Ứng dụng cần GPS để hướng dẫn bạn tham quan.", "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _locationService.LocationUpdated -= OnLocationUpdated;
        _locationService.StopTracking();
        _ = _audioService.StopAsync();
    }

    private void OnLocationUpdated(object? sender, Location userLocation)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                ZooMap.MyLocationLayer.UpdateMyLocation(
                    new Position(userLocation.Latitude, userLocation.Longitude));

                bool isNearAnyPoi = false;
                foreach (var poi in _allPois)
                {
                    // Dùng poi.Radius (nullable int) — GeofencingEngine tự xử lý null
                    if (_geofencingEngine.IsWithinRadius(userLocation, poi))
                    {
                        isNearAnyPoi = true;
                        if (_currentActivePoi?.PoiId != poi.PoiId)
                        {
                            _currentActivePoi = poi;
                            await OnEnterPoiZoneAsync(poi);
                        }
                        break;
                    }
                }

                if (!isNearAnyPoi)
                    _currentActivePoi = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] Location error: {ex.Message}");
            }
        });
    }

    private async Task OnEnterPoiZoneAsync(Poi poi)
    {
        bool wantToListen = await DisplayAlert(
            "🦁 Phát hiện khu vực!",
            $"Bạn đã đến: {poi.Name}\nMuốn nghe thuyết minh không?",
            "Nghe ngay", "Bỏ qua");

        if (wantToListen)
        {
            var audioPage = IPlatformApplication.Current.Services.GetService<StoryAudioPage>();
            if (audioPage != null)
            {
                audioPage.LoadPoi(poi);
                await Navigation.PushAsync(audioPage);
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
        => await Navigation.PopAsync();
}

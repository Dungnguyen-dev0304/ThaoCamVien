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

    // Chỉnh sửa hàm OnLocationUpdated trong MapPage.xaml.cs
    private void OnLocationUpdated(object? sender, Location userLocation)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                // 1. Cập nhật chấm xanh trên bản đồ
                ZooMap.MyLocationLayer.UpdateMyLocation(new Position(userLocation.Latitude, userLocation.Longitude));

                // 2. Geofencing: Quét xem có đang đứng gần chuồng thú nào không
                bool isNearAnyPoi = false;
                foreach (var poi in _allPois)
                {
                    if (_geofencingEngine.IsWithinRadius(userLocation, poi))
                    {
                        isNearAnyPoi = true;

                        // Nếu là POI mới (chưa phát) -> Kích hoạt Narration Engine
                        if (_currentActivePoi?.PoiId != poi.PoiId)
                        {
                            _currentActivePoi = poi;

                            // Lấy NarrationEngine từ DI và phát âm thanh
                            var narrationEngine = IPlatformApplication.Current.Services.GetService<NarrationEngine>();
                            if (narrationEngine != null)
                            {
                                await narrationEngine.PlayNarrativeAsync(poi);
                            }
                        }
                        break;
                    }
                }

                // Nếu đi ra khỏi vùng bán kính của tất cả POI, reset lại trạng thái
                if (!isNearAnyPoi)
                {
                    _currentActivePoi = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] Lỗi GPS: {ex.Message}");
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

using Mapsui.Tiling;
using Mapsui.UI.Maui;
using AppThaoCamVien.Services;
using AppThaoCamVien.ViewModels;
using SharedThaoCamVien.Models;
using Microsoft.Maui.Devices.Sensors;
using System.Diagnostics;

namespace AppThaoCamVien.Pages;

/// <summary>
/// MapPage — FIX: GPS crash khi vào vùng POI, tất cả exception được catch.
/// </summary>
public partial class MapPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly LocationService _gps;
    private readonly GeofencingEngine _geo;
    private readonly NarrationEngine _narration;
    private readonly DirectionsService _directions;
    private readonly IServiceProvider _sp;
    private readonly MapPageViewModel _vm;

    private List<Poi> _pois = [];
    private Poi? _nearPoi;
    private Poi? _focusPoi;
    private CancellationTokenSource? _dotCts;
    private bool _mapReady = false;
    private bool _routeVisible = false;

    public MapPage(DatabaseService db, LocationService gps,
                   GeofencingEngine geo, NarrationEngine narration,
                   DirectionsService directions,
                   IServiceProvider sp,
                   MapPageViewModel vm)
    {
        InitializeComponent();
        _db = db; _gps = gps; _geo = geo; _narration = narration;
        _directions = directions; _sp = sp;
        _vm = vm;
        BindingContext = _vm;
    }

    // ── Setup Map ────────────────────────────────────────────────────────
    private void SetupMap()
    {
        try
        {
            ZooMap.Map?.Layers.Add(OpenStreetMap.CreateTileLayer());
            ZooMap.Map?.Widgets.Clear();
            ZooMap.MyLocationEnabled = true;
            CenterZoo();
            _mapReady = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] SetupMap error: {ex.Message}");
        }
    }

    private void CenterZoo()
    {
        try
        {
            var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(106.7055, 10.7870);
            ZooMap.Map?.Navigator?.CenterOnAndZoomTo(new Mapsui.MPoint(x, y), 3);
        }
        catch { }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Setup map lần đầu
        if (!_mapReady) SetupMap();

        // Subscribe events
        _gps.LocationUpdated += OnLocation;
        _gps.StatusChanged += OnGpsStatus;

        StartDotBlink();

        // API-first + StateContainer: load POIs trước để tránh UI/engine race
        await LoadPoisAsync();
        if (_focusPoi != null)
        {
            CenterPoi(_focusPoi);
            ShowNearPoiPanel(_focusPoi, 0, true);
            HighlightPin(_focusPoi);
            _focusPoi = null;
        }
        await StartGpsAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _gps.LocationUpdated -= OnLocation;
        _gps.StatusChanged -= OnGpsStatus;
        _gps.Stop();
        _dotCts?.Cancel();
        _ = _narration.StopAsync();

        // Cleanup route khi rời trang
        if (_routeVisible)
        {
            DirectionsService.RemoveRouteLayer(ZooMap.Map);
            _routeVisible = false;
        }
    }

    // ── Load POIs ─────────────────────────────────────────────────────────
    private async Task LoadPoisAsync()
    {
        try
        {
            await _vm.SafeReloadAsync();
            _pois = _vm.Pois ?? [];
            _geo.SetPois(_pois);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    ZooMap.Pins.Clear();
                    foreach (var p in _pois)
                    {
                        ZooMap.Pins.Add(new Pin(ZooMap)
                        {
                            Label = p.Name ?? "---",
                            Position = new Position((double)p.Latitude, (double)p.Longitude),
                            Type = PinType.Pin,
                            Color = Colors.OrangeRed
                        });
                    }
                    BuildChips();
                    PoiCountLbl.Text = $"{_pois.Count} {GetRes("TxtPoints", "điểm")}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Map] UI update error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] LoadPois error: {ex.Message}");
        }
    }

    private void BuildChips()
    {
        try
        {
            ChipsContainer.Children.Clear();
            foreach (var poi in _pois)
            {
                var b = new Border
                {
                    BackgroundColor = Color.FromArgb("#0A2A1B"),
                    Padding = new Thickness(12, 7),
                    Stroke = Color.FromArgb("#15402A"),
                    StrokeThickness = 1
                };
                b.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 };
                b.Content = new Label
                {
                    Text = poi.Name ?? "---",
                    TextColor = Colors.White,
                    FontSize = 11
                };
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => CenterPoi(poi);
                b.GestureRecognizers.Add(tap);
                ChipsContainer.Children.Add(b);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] BuildChips error: {ex.Message}");
        }
    }

    // ── GPS ──────────────────────────────────────────────────────────────
    private async Task StartGpsAsync()
    {
        try
        {
            var ok = await _gps.CheckAndRequestPermissionAsync();
            if (!ok)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    GpsLabel.Text = "GPS bị từ chối — bản đồ chỉ hiển thị");
                return;
            }
            await _gps.StartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] StartGps error: {ex.Message}");
        }
    }

    private void OnGpsStatus(object? sender, string status)
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            try { GpsLabel.Text = status; } catch { }
        });

    private void OnLocation(object? sender, Location loc)
    {
        // KHÔNG dùng async void với heavy work — dễ crash
        // Dùng Task.Run để xử lý geofencing trên background thread
        _ = Task.Run(async () =>
        {
            try
            {
                // Cập nhật chấm vị trí trên bản đồ (phải trên MainThread)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        ZooMap.MyLocationLayer.UpdateMyLocation(
                            new Position(loc.Latitude, loc.Longitude));
                    }
                    catch { }
                });

                // Geofencing (có thể chạy trên background)
                var result = _geo.Process(loc);

                // Cập nhật UI (phải trên MainThread)
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    try
                    {
                        if (result.ActivePoi != null)
                        {
                            ShowNearPoiPanel(result.ActivePoi, result.ActiveDist, true);
                            HighlightPin(result.ActivePoi);
                        }
                        else if (result.ApproachingPois.Count > 0)
                        {
                            var n = result.ApproachingPois.OrderBy(x => x.dist).First();
                            ShowNearPoiPanel(n.poi, n.dist, false);
                        }
                        else
                        {
                            ShowDefaultPanel();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Map] UI update error: {ex.Message}");
                    }
                });

                // Auto-narration (background, không block UI)
                if (result.ActivePoi != null && result.CanTrigger)
                {
                    _geo.MarkTriggered(result.ActivePoi.PoiId);
                    try
                    {
                        await _narration.PlayAsync(result.ActivePoi, forcePlay: false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Map] Narration error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Map] OnLocation error: {ex.Message}");
            }
        });
    }

    // ── UI Panels ────────────────────────────────────────────────────────
    private void ShowNearPoiPanel(Poi poi, double dist, bool inside)
    {
        _nearPoi = poi;
        NearPoiPanel.IsVisible = true;
        DefaultPanel.IsVisible = false;
        NearPoiName.Text = poi.Name ?? "---";
        NearPoiDist.Text = $"📍 {dist:F0}m";
        NearPoiStatus.Text = inside
            ? GetRes("TxtInZone", "● Trong vùng")
            : GetRes("TxtApproaching", "○ Đang tiếp cận");
        NearPoiStatus.TextColor = inside
            ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FFCA28");

        try
        {
            NearPoiImg.Source = !string.IsNullOrEmpty(poi.ImageThumbnail)
                ? ImageSource.FromFile(poi.ImageThumbnail)
                : "placeholder_animal.png";
        }
        catch { NearPoiImg.Source = "placeholder_animal.png"; }
    }

    private void ShowDefaultPanel()
    {
        _nearPoi = null;
        NearPoiPanel.IsVisible = false;
        DefaultPanel.IsVisible = true;
    }

    private void HighlightPin(Poi? active)
    {
        try
        {
            foreach (var pin in ZooMap.Pins)
                pin.Color = (active != null && pin.Label == active.Name)
                    ? Color.FromArgb("#FFCA28") : Colors.OrangeRed;
        }
        catch { }
    }

    // ── Actions ──────────────────────────────────────────────────────────
    private async void OnListenNowTapped(object sender, TappedEventArgs e)
    {
        if (_nearPoi == null) return;
        try
        {
            _geo.ResetCooldown(_nearPoi.PoiId);
            var page = _sp.GetRequiredService<StoryAudioPage>();
            page.LoadPoi(_nearPoi);
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }

    private void CenterPoi(Poi poi)
    {
        try
        {
            var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(
                (double)poi.Longitude, (double)poi.Latitude);
            ZooMap.Map?.Navigator?.CenterOnAndZoomTo(new Mapsui.MPoint(x, y), 4);
        }
        catch { }
    }

    public void FocusPoi(Poi poi)
    {
        _focusPoi = poi;
        System.Diagnostics.Debug.WriteLine($"[MapPage] focus request poiId={poi.PoiId} name='{poi.Name}'");
    }

    private void OnMyLocationTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (_gps.LastLocation == null) return;
            var (x, y) = Mapsui.Projections.SphericalMercator.FromLonLat(
                _gps.LastLocation.Longitude, _gps.LastLocation.Latitude);
            ZooMap.Map?.Navigator?.CenterOnAndZoomTo(new Mapsui.MPoint(x, y), 4);
        }
        catch { }
    }

    private void OnCenterClicked(object sender, EventArgs e) => CenterZoo();
    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();

    // ── Directions (Route Polyline) ─────────────────────────────────────
    private async void OnDirectionsTapped(object sender, TappedEventArgs e)
    {
        if (_nearPoi == null) return;

        try
        {
            // Toggle: nếu route đang hiển thị → xoá
            if (_routeVisible)
            {
                DirectionsService.RemoveRouteLayer(ZooMap.Map);
                _routeVisible = false;
                ResetAllPins(); // Hiển thị lại tất cả pins
                return;
            }

            // Cần vị trí hiện tại làm điểm xuất phát
            var loc = _gps.LastLocation;
            if (loc == null)
            {
                await DisplayAlert(
                    GetRes("TxtMapTitle", "Bản đồ"),
                    "Chưa có vị trí GPS. Vui lòng bật GPS và thử lại.",
                    "OK");
                return;
            }

            var destLat = (double)_nearPoi.Latitude;
            var destLng = (double)_nearPoi.Longitude;

            // Hiển thị loading
            MainThread.BeginInvokeOnMainThread(() =>
                NearPoiDist.Text = "🔄 Đang tìm đường...");

            var route = await _directions.GetRouteAsync(
                loc.Latitude, loc.Longitude,
                destLat, destLng);

            if (route.Points.Count < 2)
            {
                await DisplayAlert(
                    GetRes("TxtMapTitle", "Bản đồ"),
                    "Không tìm được đường đi.",
                    "OK");
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Xoá route cũ (nếu có)
                    DirectionsService.RemoveRouteLayer(ZooMap.Map);

                    // Thêm route layer mới
                    var layer = DirectionsService.BuildRouteLayer(route.Points);
                    ZooMap.Map?.Layers.Add(layer);
                    _routeVisible = true;

                    // Cập nhật thông tin khoảng cách
                    NearPoiDist.Text = $"🗺 {route.DistanceText}"
                        + (string.IsNullOrEmpty(route.DurationText) || route.DurationText == "—"
                            ? "" : $" · {route.DurationText}");

                    // Stateful markers: chỉ hiển thị pin đích, ẩn các pin khác
                    ShowOnlySelectedPin(_nearPoi);

                    if (route.IsFallback)
                    {
                        Debug.WriteLine("[MapPage] Route is straight-line fallback (no API key).");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MapPage] Route layer error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapPage] OnDirectionsTapped error: {ex.Message}");
            await DisplayAlert("Lỗi", "Không thể tìm đường đi. Vui lòng thử lại.", "OK");
        }
    }

    // ── Stateful Map Markers ────────────────────────────────────────────

    /// <summary>
    /// Ẩn tất cả pin, chỉ hiển thị pin cho POI được chọn (màu đỏ nổi bật).
    /// Dùng khi hiển thị route — giảm visual clutter.
    /// </summary>
    private void ShowOnlySelectedPin(Poi selected)
    {
        try
        {
            ZooMap.Pins.Clear();
            ZooMap.Pins.Add(new Pin(ZooMap)
            {
                Label = selected.Name ?? "---",
                Position = new Position((double)selected.Latitude, (double)selected.Longitude),
                Type = PinType.Pin,
                Color = Color.FromArgb("#E53935") // Đỏ nổi bật
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapPage] ShowOnlySelectedPin error: {ex.Message}");
        }
    }

    /// <summary>
    /// Hiển thị lại TẤT CẢ pins (sau khi xoá route).
    /// </summary>
    private void ResetAllPins()
    {
        try
        {
            ZooMap.Pins.Clear();
            foreach (var p in _pois)
            {
                ZooMap.Pins.Add(new Pin(ZooMap)
                {
                    Label = p.Name ?? "---",
                    Position = new Position((double)p.Latitude, (double)p.Longitude),
                    Type = PinType.Pin,
                    Color = Colors.OrangeRed
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapPage] ResetAllPins error: {ex.Message}");
        }
    }

    // ── GPS dot animation ────────────────────────────────────────────────
    private void StartDotBlink()
    {
        _dotCts?.Cancel();
        _dotCts = new CancellationTokenSource();
        var tok = _dotCts.Token;
        _ = Task.Run(async () =>
        {
            while (!tok.IsCancellationRequested)
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(
                        () => GpsDot.Opacity = GpsDot.Opacity > 0.5 ? 0.15 : 1.0);
                    await Task.Delay(900, tok);
                }
                catch { break; }
            }
        }, tok);
    }

    // ── Helper ───────────────────────────────────────────────────────────
    private static string GetRes(string key, string fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is string s)
            return s;
        return fallback;
    }
}

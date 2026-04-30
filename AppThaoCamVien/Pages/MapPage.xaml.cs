using Mapsui.Tiling;
using Mapsui.UI.Maui;
using AppThaoCamVien.Services;
using AppThaoCamVien.ViewModels;
using SharedThaoCamVien.Models;
using Microsoft.Maui.Devices.Sensors;
using System.Diagnostics;

namespace AppThaoCamVien.Pages;

/// <summary>
/// Bản đồ Thảo Cầm Viên: hiển thị POI, GPS tracking, geofencing, chỉ đường.
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
    private Poi? _selectedPoi; // POI đang được chọn (chỉ hiện pin này)
    private CancellationTokenSource? _dotCts;
    private bool _mapReady = false;
    private bool _routeVisible = false;
    private readonly SemaphoreSlim _locationGate = new(1, 1);

    // Màu sắc đẹp cho các loại POI
    private static readonly Color PinColorDefault = Color.FromArgb("#4CAF50");   // Xanh lá
    private static readonly Color PinColorSelected = Color.FromArgb("#FF6D00");  // Cam nổi bật
    private static readonly Color PinColorHistoric = Color.FromArgb("#7B1FA2");  // Tím - di tích
    private static readonly Color PinColorAnimal = Color.FromArgb("#2E7D32");    // Xanh đậm - động vật
    private static readonly Color PinColorPlant = Color.FromArgb("#00897B");     // Xanh ngọc - thực vật

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
            ZooMap.PinClicked += OnPinClicked;
            CenterZoo();
            _mapReady = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] SetupMap error: {ex.Message}");
        }
    }

    /// <summary>
    /// Khi bấm vào 1 pin: nếu chưa chọn → chỉ hiện pin đó, ẩn các pin khác.
    /// Nếu đã chọn pin đó rồi → hiện lại tất cả.
    /// </summary>
    private void OnPinClicked(object? sender, PinClickedEventArgs e)
    {
        try
        {
            var clickedPin = e.Pin;
            if (clickedPin == null) return;

            // Tìm POI tương ứng với pin được bấm
            var clickedPoi = _pois.FirstOrDefault(p => p.Name == clickedPin.Label);
            if (clickedPoi == null) return;

            // Nếu đang chọn POI này rồi → toggle: hiện lại tất cả
            if (_selectedPoi != null && _selectedPoi.PoiId == clickedPoi.PoiId)
            {
                _selectedPoi = null;
                ResetAllPins();
                ShowDefaultPanel();
                e.Handled = true;
                return;
            }

            // Chọn POI mới → chỉ hiện pin này
            _selectedPoi = clickedPoi;
            ShowOnlySelectedPin(clickedPoi);
            ShowNearPoiPanel(clickedPoi, 0, true);
            CenterPoi(clickedPoi);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Map] OnPinClicked error: {ex.Message}");
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
        _narration.QueueChanged += OnNarrationQueueChanged;

        // Cập nhật panel queue lần đầu (có thể đang có audio phát từ trang trước)
        UpdateQueueUI();

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
        _narration.QueueChanged -= OnNarrationQueueChanged;
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
                    _selectedPoi = null;
                    ResetAllPins();
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
                var pinColor = GetPinColor(poi);
                var b = new Border
                {
                    BackgroundColor = pinColor,
                    Padding = new Thickness(14, 8),
                    StrokeThickness = 0
                };
                b.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 };
                b.Shadow = new Shadow { Brush = pinColor, Opacity = 0.3f, Radius = 6, Offset = new Point(0, 3) };
                b.Content = new Label
                {
                    Text = poi.Name ?? "---",
                    TextColor = Colors.White,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold
                };
                var tap = new TapGestureRecognizer();
                var capturedPoi = poi;
                tap.Tapped += (_, _) =>
                {
                    // Bấm chip = chọn POI đó (toggle)
                    if (_selectedPoi != null && _selectedPoi.PoiId == capturedPoi.PoiId)
                    {
                        _selectedPoi = null;
                        ResetAllPins();
                        ShowDefaultPanel();
                    }
                    else
                    {
                        _selectedPoi = capturedPoi;
                        ShowOnlySelectedPin(capturedPoi);
                        ShowNearPoiPanel(capturedPoi, 0, true);
                        CenterPoi(capturedPoi);
                    }
                };
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
            Poi? poiToAutoPlay = null;
            try
            {
                // Serialize location processing để tránh race khi fake/teleport GPS
                // (2 update tới gần nhau có thể chạy chồng và đảo thứ tự).
                await _locationGate.WaitAsync();

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

                            // ✅ Prefetch audio nền: user đang tiếp cận → tải trước
                            //    để khi bước vào bán kính là phát ngay, không lag.
                            //    NarrationEngine dedupe theo {poiId}_{lang} nên gọi
                            //    nhiều lần vô hại.
                            foreach (var (poi, _) in result.ApproachingPois)
                                _narration.PrefetchAudio(poi.PoiId);
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
                    // Không await ở đây: nếu await thì location tiếp theo (B) sẽ bị chặn
                    // bởi _locationGate cho tới khi A phát xong, làm queue không enqueue được.
                    poiToAutoPlay = result.ActivePoi;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Map] OnLocation error: {ex.Message}");
            }
            finally
            {
                try { _locationGate.Release(); } catch { }
            }

            // Gọi narration sau khi đã release gate để không chặn các location tiếp theo.
            if (poiToAutoPlay != null)
            {
                try
                {
                    _ = _narration.PlayAsync(poiToAutoPlay, forcePlay: false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Map] Narration enqueue error: {ex.Message}");
                }
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
            {
                if (active != null && pin.Label == active.Name)
                {
                    pin.Color = PinColorSelected;
                }
                else
                {
                    var poi = _pois.FirstOrDefault(p => p.Name == pin.Label);
                    pin.Color = poi != null ? GetPinColor(poi) : PinColorDefault;
                }
            }
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

    // ── Pin Styling ────────────────────────────────────────────────────

    /// <summary>
    /// Chọn màu pin dựa trên CategoryId.
    /// 1 = Di tích, 2 = Động vật, 3 = Thực vật (convention từ API).
    /// </summary>
    private static Color GetPinColor(Poi poi) => poi.CategoryId switch
    {
        1 => PinColorHistoric,
        2 => PinColorAnimal,
        3 => PinColorPlant,
        _ => PinColorDefault
    };

    /// <summary>Chỉ hiện 1 pin được chọn, ẩn tất cả pin khác.</summary>
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
                Color = PinColorSelected
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapPage] ShowOnlySelectedPin error: {ex.Message}");
        }
    }

    /// <summary>Hiện lại tất cả pins với màu theo category.</summary>
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
                    Color = GetPinColor(p)
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

    // ═════════════════════════════════════════════════════════════════════
    // QUEUE UI — panel "Đang phát + Tiếp theo" + nút Skip / Stop / ✕ (remove)
    // Lắng nghe NarrationEngine.QueueChanged, rebuild UI mỗi khi queue đổi.
    // ═════════════════════════════════════════════════════════════════════

    private void OnNarrationQueueChanged(object? sender, EventArgs e)
    {
        // Event có thể fire từ background thread (Task.Run trong drain loop),
        // MainThread.BeginInvokeOnMainThread marshal về UI thread để update an toàn.
        MainThread.BeginInvokeOnMainThread(UpdateQueueUI);
    }

    private void UpdateQueueUI()
    {
        try
        {
            var currentId = _narration.CurrentPoiId;
            var queued = _narration.GetQueueSnapshot();

            // Ẩn hoàn toàn panel nếu không đang phát gì và queue rỗng
            if (currentId < 0 && queued.Count == 0)
            {
                QueuePanel.IsVisible = false;
                return;
            }

            QueuePanel.IsVisible = true;
            // Chỉ hiện nút Skip khi có POI "tiếp theo" trong queue
            QueueSkipBtn.IsVisible = queued.Count > 0;

            // Tên POI đang phát
            if (currentId > 0)
            {
                var playing = _pois.FirstOrDefault(p => p.PoiId == currentId);
                QueueNowPlayingLbl.Text = playing?.Name ?? $"POI #{currentId}";
            }
            else
            {
                QueueNowPlayingLbl.Text = "—";
            }

            // Rebuild chip list cho queue
            QueueChipsContainer.Children.Clear();
            foreach (var poi in queued)
            {
                QueueChipsContainer.Children.Add(BuildQueueChip(poi));
            }
            QueueListRow.IsVisible = queued.Count > 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Map] UpdateQueueUI error: {ex.Message}");
        }
    }

    private Border BuildQueueChip(Poi poi)
    {
        // Chip: [Tên POI] [✕]
        var nameLbl = new Label
        {
            Text = poi.Name ?? $"POI #{poi.PoiId}",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1A1A1A"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        };

        var removeLbl = new Label
        {
            Text = "✕",
            FontSize = 12,
            TextColor = Color.FromArgb("#999999"),
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(4, 0, 2, 0)
        };
        var tap = new TapGestureRecognizer();
        var capturedPoiId = poi.PoiId;
        tap.Tapped += (_, _) => OnQueueItemRemoveTapped(capturedPoiId);
        removeLbl.GestureRecognizers.Add(tap);

        var layout = new HorizontalStackLayout { Spacing = 4 };
        layout.Children.Add(nameLbl);
        layout.Children.Add(removeLbl);

        return new Border
        {
            BackgroundColor = Color.FromArgb("#F0F9F4"),
            Padding = new Thickness(10, 6),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(10)
            },
            Content = layout
        };
    }

    private async void OnQueueSkipTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await _narration.SkipAsync();
            // UI tự cập nhật qua QueueChanged event sau khi drain loop advance.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Map] Skip error: {ex.Message}");
        }
    }

    private async void OnQueueStopTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            await _narration.StopAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Map] Stop error: {ex.Message}");
        }
    }

    private void OnQueueItemRemoveTapped(int poiId)
    {
        try
        {
            _narration.RemoveFromQueue(poiId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Map] RemoveFromQueue error: {ex.Message}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // DEMO QUEUE — chỉ trong build DEBUG. Giả lập GPS approach 2 POI liên tiếp.
    // Dùng để demo hàng đợi mà không cần đi bộ thật hoặc fake GPS app.
    // ═════════════════════════════════════════════════════════════════════

    private async void OnDemoQueueTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (_pois.Count < 2)
            {
                await DisplayAlert("Demo hàng đợi", "Cần ít nhất 2 POI trong danh sách để demo.", "OK");
                return;
            }

            // Lấy 2 POI đầu danh sách. Reset cooldown để demo chạy lại được
            // nhiều lần trong cùng 1 phiên (không thì lần 2 sẽ bị debounce).
            var poiA = _pois[0];
            var poiB = _pois[1];
            _narration.ResetCooldown(poiA.PoiId);
            _narration.ResetCooldown(poiB.PoiId);

            Debug.WriteLine($"[Demo] simulate approach A='{poiA.Name}' then B='{poiB.Name}'");

            // Bước 1: approach POI A → GPS sẽ gọi PlayAsync(poi, false).
            // Ta gọi đúng như GPS — forcePlay=false, không ngắt gì.
            _ = _narration.PlayAsync(poiA, forcePlay: false);

            // Cho audio A kịp bắt đầu phát (tải MP3 / init TTS)
            await Task.Delay(3000);

            // Bước 2: approach POI B trong lúc A còn đang phát.
            // forcePlay=false + lock đã bị drain loop của A giữ → B được enqueue.
            Debug.WriteLine("[Demo] now approaching B (should enqueue)");
            _ = _narration.PlayAsync(poiB, forcePlay: false);

            // Panel "Tiếp theo: {B.Name}" sẽ tự xuất hiện nhờ QueueChanged event.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Demo] OnDemoQueueTapped error: {ex.Message}");
        }
    }
}

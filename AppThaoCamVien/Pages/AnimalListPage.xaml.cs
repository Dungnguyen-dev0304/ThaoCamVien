using SharedThaoCamVien.Models;
using System.Collections.ObjectModel;
using AppThaoCamVien.Services;

namespace AppThaoCamVien.Pages;

/// <summary>
/// Trang danh sách động vật — hiển thị dữ liệu thật từ API/SQLite.
/// Hỗ trợ tìm kiếm và tự làm mới mỗi khi ngôn ngữ thay đổi.
/// </summary>
public partial class AnimalListPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly IServiceProvider _serviceProvider;

    // ObservableCollection tự động cập nhật UI khi thêm/xóa item
    public ObservableCollection<Poi> AnimalsList { get; set; } = new();

    // Giữ danh sách đầy đủ để filter không mất dữ liệu gốc
    private List<Poi> _allPois = new();

    // Theo dõi ngôn ngữ cũ để biết khi nào cần reload
    private string _lastLoadedLanguage = "";

    public AnimalListPage(DatabaseService databaseService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _serviceProvider = serviceProvider;

        AnimalsCollectionView.ItemsSource = AnimalsList;

        // Gắn sự kiện tìm kiếm — TextChanged là real-time, không cần nhấn Enter
        SearchEntry.TextChanged += OnSearchTextChanged;
    }

    // =====================================================================
    // LIFECYCLE
    // =====================================================================
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Chỉ reload khi ngôn ngữ thay đổi hoặc lần đầu mở trang
        // Điều này tránh gọi API mỗi lần user chuyển tab
        var currentLang = _databaseService.CurrentLanguage;
        if (currentLang != _lastLoadedLanguage)
        {
            _lastLoadedLanguage = currentLang;
            await LoadDataAsync();
        }
    }

    // =====================================================================
    // LẤY DỮ LIỆU THẬT TỪ API → SQLITE → FALLBACK OFFLINE
    // =====================================================================
    private async Task LoadDataAsync()
    {
        try
        {
            // 1. Sync từ API (có xử lý lỗi mạng bên trong, không crash app)
            await _databaseService.SyncDataFromApiAsync();

            // 2. Lấy dữ liệu từ SQLite local (đã có bản dịch đúng ngôn ngữ từ API)
            var pois = await _databaseService.GetAllPoisAsync();
            _allPois = pois;

            // 3. Cập nhật UI trên Main Thread (bắt buộc — tránh exception cross-thread)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AnimalsList.Clear();
                foreach (var poi in pois)
                    AnimalsList.Add(poi);

                // Clear ô tìm kiếm khi reload dữ liệu mới
                SearchEntry.Text = "";
            });

            System.Diagnostics.Debug.WriteLine(
                $"[AnimalListPage] Loaded {pois.Count} POIs (lang: {_databaseService.CurrentLanguage})");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi tải dữ liệu", ex.Message, "OK");
        }
    }

    // =====================================================================
    // TÌM KIẾM REAL-TIME
    // =====================================================================
    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim().ToLower() ?? "";

        MainThread.BeginInvokeOnMainThread(() =>
        {
            AnimalsList.Clear();

            // Nếu ô tìm kiếm trống → hiện tất cả
            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allPois
                : _allPois.Where(p =>
                    (p.Name?.ToLower().Contains(query) ?? false) ||
                    (p.Description?.ToLower().Contains(query) ?? false));

            foreach (var poi in filtered)
                AnimalsList.Add(poi);
        });
    }

    // =====================================================================
    // NHẤN VÀO THẺ ĐỘNG VẬT → MỞ STORYAUDIOPAGE
    // =====================================================================
    private async void OnAnimalTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not Poi selectedAnimal) return;

        // Hiệu ứng nhấp nháy UX
        if (sender is View view)
        {
            await view.FadeTo(0.5, 80);
            await view.FadeTo(1.0, 80);
        }

        try
        {
            var storyPage = _serviceProvider.GetService<StoryAudioPage>();
            if (storyPage != null)
            {
                storyPage.LoadPoi(selectedAnimal);
                await Navigation.PushAsync(storyPage);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể mở trang thuyết minh: {ex.Message}", "OK");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
        => await Navigation.PopAsync();
}

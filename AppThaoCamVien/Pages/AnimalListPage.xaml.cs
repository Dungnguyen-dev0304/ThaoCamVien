using SharedThaoCamVien.Models;
using System.Collections.ObjectModel;
using AppThaoCamVien.Services;

namespace AppThaoCamVien.Pages;

/// <summary>
/// AnimalListPage — danh sách 15 POI thật từ DB/API.
/// FIX: Constructor chỉ nhận DatabaseService và IServiceProvider (không có NarrationEngine
/// hay các service nặng khác để tránh circular dependency khi Shell init).
/// </summary>
public partial class AnimalListPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly IServiceProvider _sp;

    public ObservableCollection<Poi> Animals { get; } = new();
    private List<Poi> _all = [];
    private string _loadedLang = "";
    private bool _isFirstLoad = true;

    public AnimalListPage(DatabaseService db, IServiceProvider sp)
    {
        InitializeComponent();
        _db = db;
        _sp = sp;
        BindingContext = this;
        AnimalsCollectionView.ItemsSource = Animals;
        SearchEntry.TextChanged += OnSearch;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Reload khi ngôn ngữ thay đổi hoặc lần đầu vào trang
        var lang = _db.CurrentLanguage;
        if (lang != _loadedLang || _isFirstLoad)
        {
            _isFirstLoad = false;
            _loadedLang = lang;
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            // Hiện loading indicator
            if (Animals.Count == 0)
            {
                Animals.Add(new Poi { Name = "Đang tải dữ liệu...", Description = "Vui lòng chờ" });
            }

            // Sync từ API (có fallback offline nếu mất mạng)
            await _db.SyncDataFromApiAsync();

            // Lấy dữ liệu từ SQLite
            _all = await _db.GetAllPoisAsync();

            // Cập nhật UI trên Main Thread — BẮT BUỘC
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Animals.Clear();
                foreach (var p in _all)
                    Animals.Add(p);
                SearchEntry.Text = "";
            });

            System.Diagnostics.Debug.WriteLine($"[AnimalList] Loaded {_all.Count} POIs");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnimalList] Load error: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Animals.Clear();
                // Hiển thị lỗi thay vì crash
                DisplayAlertAsync("Lỗi tải dữ liệu",
                    $"Không thể tải danh sách. Vui lòng thử lại.\n{ex.Message}", "OK");
            });
        }
    }

    private void OnSearch(object? sender, TextChangedEventArgs e)
    {
        var q = (e.NewTextValue ?? "").Trim().ToLower();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Animals.Clear();
            var src = string.IsNullOrEmpty(q)
                ? _all
                : _all.Where(p =>
                    (p.Name?.ToLower().Contains(q) ?? false) ||
                    (p.Description?.ToLower().Contains(q) ?? false));
            foreach (var p in src) Animals.Add(p);
        });
    }

    private async void OnAnimalTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not Poi poi) return;

        // Hiệu ứng nhấp nháy
        if (sender is View v)
        {
            await v.FadeToAsync(0.5, 80);
            await v.FadeToAsync(1.0, 80);
        }

        try
        {
            // Lấy StoryAudioPage qua ServiceProvider (DI đúng cách)
            var page = _sp.GetRequiredService<StoryAudioPage>();
            page.LoadPoi(poi);
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", $"Không thể mở trang thuyết minh:\n{ex.Message}", "OK");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
        => await Navigation.PopAsync();
}

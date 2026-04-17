using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels.Core;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AppThaoCamVien.ViewModels;

public sealed class AnimalsViewModel : INotifyPropertyChanged
{
    private readonly ApiService _api;
    private readonly DatabaseService _db;

    /// <summary>Bảng dịch category: Vi ↔ En.</summary>
    private static readonly (string Vi, string En)[] CategoryTranslations =
    [
        ("Tất cả",     "All"),
        ("Di tích",    "Historic Sites"),
        ("Động vật",   "Animals"),
        ("Thực vật",   "Plants"),
        ("Bò sát",     "Reptiles"),
        ("Chim",       "Birds"),
        ("Cá",         "Fish"),
        ("Linh trưởng","Primates"),
        ("Thú",        "Mammals"),
        ("Côn trùng",  "Insects"),
    ];

    private static readonly Dictionary<string, string> NameTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Voi Châu Á"]             = "Asian Elephant",
        ["Hổ Bengal"]              = "Bengal Tiger",
        ["Hươu cao cổ"]           = "Giraffe",
        ["Gấu Ngựa"]              = "Sun Bear",
        ["Hà Mã"]                 = "Hippopotamus",
        ["Chuồng khỉ"]            = "Monkey House",
        ["Khỉ sóc"]               = "Squirrel Monkey",
        ["Chuồng cá sấu"]         = "Crocodile Enclosure",
        ["Khu bò sát"]            = "Reptile Zone",
        ["Vườn bướm"]             = "Butterfly Garden",
        ["Nhà hoa lan kiểng"]     = "Orchid & Bonsai House",
        ["Cây di sản"]            = "Heritage Tree",
        ["Đền Hùng"]              = "Hung Kings Temple",
        ["Bảo tàng"]              = "Museum",
        ["Quán Nhân Hương"]       = "Nhan Huong Café",
        ["Khu chuồng thú (Demo)"] = "Animal House (Demo)",
        ["Công viên Thảo Cầm Viên"] = "Saigon Zoo & Botanical Gardens",
    };

    /// <summary>Dịch tên POI sang EN nếu có trong bảng dịch.</summary>
    private static string TranslateName(string name, string lang)
    {
        if (lang != "en" || string.IsNullOrWhiteSpace(name)) return name;
        return NameTranslations.TryGetValue(name, out var en) ? en : name;
    }

    public AnimalsViewModel(ApiService api, DatabaseService db)
    {
        _api = api;
        _db = db;
        RetryText = "Retry";
        LoadCommand = new Command(async () => await LoadAsync());
    }

    /// <summary>Dịch category sang ngôn ngữ hiện tại. Giữ nguyên nếu không có bản dịch.</summary>
    private static string TranslateCategory(string text, string lang)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        foreach (var (vi, en) in CategoryTranslations)
        {
            if (lang == "en" && text.Equals(vi, StringComparison.OrdinalIgnoreCase)) return en;
            if (lang != "en" && text.Equals(en, StringComparison.OrdinalIgnoreCase)) return vi;
        }
        return text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private string _retryText = "Retry";
    public string RetryText
    {
        get => _retryText;
        set => SetProperty(ref _retryText, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplyFilters();
        }
    }

    private AnimalFilterChip? _selectedFilter;
    public AnimalFilterChip? SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                foreach (var f in Filters)
                    f.IsSelected = ReferenceEquals(f, value);

                ApplyFilters();
            }
        }
    }

    public ObservableCollection<Animal> AllAnimals { get; } = new();
    public ObservableCollection<Animal> VisibleAnimals { get; } = new();
    public ObservableCollection<AnimalFilterChip> Filters { get; } = new();

    public ICommand LoadCommand { get; }

    private async Task LoadAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;

            _api.RefreshBaseUrlFromPreferences();
            _db.RefreshApiBaseUrl();
            await _db.SyncDataFromApiAsync();

            var lang = LanguageManager.Current;
            var dto = await _api.GetAsync<AnimalsResponseDto>($"{ApiEndpoints.Animals}?lang={lang}");

            AllAnimals.Clear();
            VisibleAnimals.Clear();
            Filters.Clear();

            if (dto?.Animals != null && dto.Animals.Count > 0)
            {
                // ── API có dữ liệu → dùng trực tiếp, dịch nếu cần ──
                foreach (var a in dto.Animals)
                {
                    AllAnimals.Add(new Animal(
                        id: a.Id,
                        name: TranslateName(a.Name, lang),
                        category: TranslateCategory(a.Category, lang),
                        conservationStatus: a.ConservationStatus,
                        statusColorHex: a.StatusColorHex,
                        imageUrl: a.ImageUrl));
                }

                // Không phụ thuộc hoàn toàn dto.Filters từ backend:
                // luôn dựng danh mục từ dữ liệu động vật thực tế để tránh trường hợp app chỉ hiện 1 chip.
                BuildFiltersFromAnimals(lang, dto.Filters);
            }
            else
            {
                // ── API không có dữ liệu → fallback SQLite local ──────
                System.Diagnostics.Debug.WriteLine("[AnimalsVM] API returned null/empty → using local SQLite.");
                await LoadFromLocalDbAsync();
            }

            if (AllAnimals.Count == 0)
            {
                HasError = true;
                ErrorMessage = lang == "en"
                    ? "Could not load the animal list. Please check your connection and try again."
                    : "Không tải được danh sách động vật. Hãy kiểm tra kết nối mạng và thử lại.";
                return;
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AnimalsVM] LoadAsync error: {ex.Message} → trying local.");
            // Thử fallback local khi API crash
            try
            {
                await LoadFromLocalDbAsync();
                if (AllAnimals.Count > 0)
                {
                    ApplyFilters();
                    HasError = false;
                    return;
                }
            }
            catch { }

            HasError = true;
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Đã xảy ra lỗi. Vui lòng thử lại."
                : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Map CategoryId → tên category tiếng Việt (dùng cho SQLite fallback).</summary>
    private static string GetCategoryNameVi(int? categoryId) => categoryId switch
    {
        1 => "Di tích",
        2 => "Động vật",
        3 => "Thực vật",
        _ => "Động vật"
    };

    /// <summary>
    /// Tạo filter chip từ dữ liệu động vật đang có.
    /// Ưu tiên thứ tự từ backend (nếu có), sau đó thêm các category còn thiếu.
    /// </summary>
    private void BuildFiltersFromAnimals(string lang, IReadOnlyList<AnimalFilterDto>? backendFilters = null)
    {
        Filters.Clear();

        var allTitle = lang == "en" ? "All" : "Tất cả";
        AnimalFilterChip? allChip = null;
        allChip = new AnimalFilterChip(
            title: allTitle,
            category: null,
            selectCommand: new Command(() => SelectedFilter = allChip));
        Filters.Add(allChip);

        var categories = new List<string>();

        // 1) Lấy thứ tự từ backend trước (nếu backend có trả)
        if (backendFilters != null)
        {
            foreach (var f in backendFilters)
            {
                if (string.IsNullOrWhiteSpace(f.Category))
                    continue;

                var c = TranslateCategory(f.Category, lang);
                if (!categories.Contains(c, StringComparer.OrdinalIgnoreCase))
                    categories.Add(c);
            }
        }

        // 2) Bổ sung các category có trong dữ liệu app nhưng backend filter thiếu
        foreach (var c in AllAnimals.Select(a => a.Category)
                     .Where(s => !string.IsNullOrWhiteSpace(s))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!categories.Contains(c, StringComparer.OrdinalIgnoreCase))
                categories.Add(c);
        }

        foreach (var c in categories)
        {
            AnimalFilterChip? chip = null;
            chip = new AnimalFilterChip(
                title: c,
                category: c,
                selectCommand: new Command(() => SelectedFilter = chip));
            Filters.Add(chip);
        }

        SelectedFilter = allChip;
    }

    /// <summary>
    /// Fallback: tải danh sách từ SQLite local khi API không khả dụng.
    /// </summary>
    private async Task LoadFromLocalDbAsync()
    {
        var localPois = await _db.GetAllPoisAsync();
        if (localPois.Count == 0) return;

        var lang = LanguageManager.Current;

        // Tạo filter chips từ các CategoryId có trong dữ liệu local
        if (Filters.Count == 0)
        {
            AnimalFilterChip? allChip = null;
            var allTitle = lang == "en" ? "All" : "Tất cả";
            allChip = new AnimalFilterChip(
                title: allTitle,
                category: null,
                selectCommand: new Command(() => SelectedFilter = allChip));
            Filters.Add(allChip);

            // Tạo filter chip cho mỗi category có trong dữ liệu
            var distinctCategories = localPois
                .Select(p => p.CategoryId)
                .Where(id => id.HasValue)
                .Distinct()
                .OrderBy(id => id);

            foreach (var catId in distinctCategories)
            {
                var catNameVi = GetCategoryNameVi(catId);
                var catName = TranslateCategory(catNameVi, lang);
                AnimalFilterChip? chip = null;
                chip = new AnimalFilterChip(
                    title: catName,
                    category: catName,
                    selectCommand: new Command(() => SelectedFilter = chip));
                Filters.Add(chip);
            }

            SelectedFilter = allChip;
        }

        var statusLabel = lang == "en" ? "Saigon Zoo" : "Thảo Cầm Viên";

        foreach (var p in localPois.OrderByDescending(x => x.Priority))
        {
            var categoryVi = GetCategoryNameVi(p.CategoryId);
            AllAnimals.Add(new Animal(
                id: p.PoiId,
                name: TranslateName(p.Name ?? "---", lang),
                category: TranslateCategory(categoryVi, lang),
                conservationStatus: statusLabel,
                statusColorHex: "#2E7D32",
                imageUrl: p.ImageThumbnail ?? ""));
        }
    }

    private void ApplyFilters()
    {
        var q = (SearchText ?? string.Empty).Trim();
        var category = SelectedFilter?.Category;

        var filtered = AllAnimals.Where(a =>
        {
            var okCategory = string.IsNullOrWhiteSpace(category) ||
                              string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase);
            if (!okCategory) return false;

            if (string.IsNullOrWhiteSpace(q)) return true;

            return (a.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || (a.Category?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        }).ToList();

        VisibleAnimals.Clear();
        foreach (var a in filtered)
            VisibleAnimals.Add(a);
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class Animal
{
    public int Id { get; }
    public string Name { get; }
    public string Category { get; }
    public string ConservationStatus { get; }
    public Color StatusColor { get; }
    public string ImageUrl { get; }

    public Animal(int id, string name, string category, string conservationStatus, string statusColorHex, string imageUrl)
    {
        Id = id;
        Name = name;
        Category = category;
        ConservationStatus = conservationStatus;
        StatusColor = Color.FromArgb(statusColorHex);
        ImageUrl = imageUrl;
    }
}

public sealed class AnimalFilterChip : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; }
    public string? Category { get; }
    public ICommand SelectCommand { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public AnimalFilterChip(string title, string? category, ICommand selectCommand)
    {
        Title = title;
        Category = category;
        SelectCommand = selectCommand;
    }
}

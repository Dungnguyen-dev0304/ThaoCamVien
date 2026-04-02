using SharedThaoCamVien.Models;
using System.Collections.ObjectModel;
using AppThaoCamVien.Services;

namespace AppThaoCamVien.Pages;

public partial class AnimalListPage : ContentPage
{
    private readonly DatabaseService _databaseService;

    // Danh sách để bind ra giao diện
    public ObservableCollection<Poi> AnimalsList { get; set; } = new ObservableCollection<Poi>();

    // Sử dụng Dependency Injection để truyền DatabaseService vào
    public AnimalListPage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;

        // Gán nguồn dữ liệu cho CollectionView
        AnimalsCollectionView.ItemsSource = AnimalsList;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRealDataAsync();
    }

    private async Task LoadRealDataAsync()
    {
        // 1. Xóa danh sách cũ mỗi khi mở lại trang
        AnimalsList.Clear();

        // 2. Lấy dữ liệu thật từ SQLite (chỉ lấy các POI đang active)
        var pois = await _databaseService.GetAllPoisAsync();

        // 3. Đưa vào danh sách hiển thị
        foreach (var poi in pois)
        {
            // Nếu bạn chỉ muốn hiển thị "Động vật" thì kiểm tra: if (poi.CategoryId == 1)
            AnimalsList.Add(poi);
        }
    }

    // Sự kiện khi nhấn vào MỘT THẺ CON VẬT
    private async void OnAnimalTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Poi selectedAnimal)
        {
            // Hiệu ứng UX
            if (sender is View view)
            {
                await view.FadeTo(0.5, 100);
                await view.FadeTo(1, 100);
            }

            // Gọi StoryAudioPage và truyền dữ liệu sang
            var storyPage = Handler?.MauiContext?.Services.GetService<StoryAudioPage>();
            if (storyPage != null)
            {
                storyPage.LoadPoi(selectedAnimal);
                await Navigation.PushAsync(storyPage);
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
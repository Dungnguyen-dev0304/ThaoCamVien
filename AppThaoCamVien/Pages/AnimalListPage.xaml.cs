using SharedThaoCamVien.Models;
using System.Collections.ObjectModel;
using AppThaoCamVien.Services;

namespace AppThaoCamVien.Pages;

public partial class AnimalListPage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly IServiceProvider _serviceProvider; // Dùng cái này để chống Crash

    public ObservableCollection<Poi> AnimalsList { get; set; } = new ObservableCollection<Poi>();

    public AnimalListPage(DatabaseService databaseService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _serviceProvider = serviceProvider;
        AnimalsCollectionView.ItemsSource = AnimalsList;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        // 1. Đảm bảo có dữ liệu
        await _databaseService.SyncDataFromApiAsync();

        // 2. Lấy dữ liệu từ DB
        var pois = await _databaseService.GetAllPoisAsync();

        // 3. Đưa lên UI (Bắt buộc chạy trên MainThread để không bị màn hình trắng)
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AnimalsList.Clear();
            foreach (var poi in pois)
            {
                AnimalsList.Add(poi);
            }
        });
    }

    private async void OnAnimalTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Poi selectedAnimal)
        {
            // Hiệu ứng nhấp nháy
            if (sender is View view)
            {
                await view.FadeTo(0.5, 100);
                await view.FadeTo(1, 100);
            }

            try
            {
                // CÁCH GỌI TRANG AN TOÀN CHỐNG CRASH
                var storyPage = _serviceProvider.GetService<StoryAudioPage>();
                if (storyPage != null)
                {
                    storyPage.LoadPoi(selectedAnimal);
                    await Navigation.PushAsync(storyPage);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi Hệ Thống", $"Không thể mở câu chuyện: {ex.Message}", "OK");
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();
}
using AppThaoCamVien.ViewModels;
using AppThaoCamVien.Services;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class AnimalsPage : ContentPage
{
    private readonly IServiceProvider _sp;
    private readonly DatabaseService _db;
    private readonly ApiService _api;

    public AnimalsPage(AnimalsViewModel vm, IServiceProvider sp, DatabaseService db, ApiService api)
    {
        InitializeComponent();
        _sp = sp;
        _db = db;
        _api = api;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (BindingContext is AnimalsViewModel vm && vm.LoadCommand.CanExecute(null))
                vm.LoadCommand.Execute(null);
        }
        catch { }
    }

    private async void OnAnimalsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (sender is CollectionView cv) cv.SelectedItem = null;

            if (e.CurrentSelection?.FirstOrDefault() is not Animal animal)
                return;

            Poi? poi = await _db.GetPoiByIdAsync(animal.Id);
            if (poi == null)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimalsPage] local miss poiId={animal.Id} -> API fallback");
                // Fallback: cache local chưa kịp sync thì lấy trực tiếp từ API.
                poi = await _api.GetAsync<Poi>($"/api/Pois/{animal.Id}?lang={LanguageManager.Current}");
                if (poi != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[AnimalsPage] API fallback hit poiId={animal.Id}, save cache");
                    await _db.SavePoiAsync(poi);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[AnimalsPage] local hit poiId={animal.Id}");
            }

            if (poi == null)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimalsPage] miss both local/api poiId={animal.Id}");
                await DisplayAlert("Thông báo", "Không tìm thấy thông tin chi tiết của loài này.", "OK");
                return;
            }

            var page = _sp.GetRequiredService<StoryAudioPage>();
            page.LoadPoi(poi);
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }
}


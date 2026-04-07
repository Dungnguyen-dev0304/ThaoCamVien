using AppThaoCamVien.Services;
using AppThaoCamVien.Services.Api;
using AppThaoCamVien.ViewModels;
using SharedThaoCamVien.Models;

namespace AppThaoCamVien.Pages;

public partial class HomePage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly IServiceProvider _sp;
    private readonly HomePageViewModel _vm;
    private int _langIdx = 0;

    public HomePage(HomePageViewModel vm, DatabaseService db, IServiceProvider sp)
    {
        InitializeComponent();
        _vm = vm;
        _db = db;
        _sp = sp;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            // Sync ngôn ngữ UI + data sync API-first
            var cur = LanguageManager.Current;
            _langIdx = LanguageManager.Languages.FindIndex(l => l.Code == cur);
            if (_langIdx < 0) _langIdx = 0;
            UpdateLangBtn();
            await _vm.SafeReloadAsync();
        }
        catch (Exception ex)
        {
            // Không crash — giữ màn hình ổn định
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnAppearing error: {ex.Message}");
        }
    }

    private async void OnLangTapped(object sender, TappedEventArgs e)
    {
        _langIdx = (_langIdx + 1) % LanguageManager.Languages.Count;
        var (code, _, _) = LanguageManager.Languages[_langIdx];
        _db.CurrentLanguage = code;
        LanguageManager.Apply(code);
        UpdateLangBtn();
        _ = _vm.SafeReloadAsync();
    }

    private void UpdateLangBtn()
    {
        var (_, flag, label) = LanguageManager.Languages[_langIdx];
        // Nhãn nhỏ chỉ là hiển thị — không ảnh hưởng logic ngôn ngữ
        LangLbl.Text = $"{flag} {label}";
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        var page = _sp.GetService<MapPage>();
        if (page != null) await Navigation.PushAsync(page);
    }

    private async void OnNowSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is HomeCardDto card)
        {
            ((CollectionView)sender).SelectedItem = null;
            await OpenPoi(card.Id);
        }
    }

    private async void OnRecommendedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is HomeCardDto card)
        {
            ((CollectionView)sender).SelectedItem = null;
            await OpenPoi(card.Id);
        }
    }

    private async Task OpenPoi(int poiId)
    {
        try
        {
            var poi = await _db.GetPoiByIdAsync(poiId);
            if (poi == null)
            {
                await DisplayAlertAsync("Thông báo", "Không tìm thấy điểm tham quan.", "OK");
                return;
            }

            var page = _sp.GetRequiredService<StoryAudioPage>();
            page.LoadPoi(poi);
            await Navigation.PushAsync(page);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", ex.Message, "OK");
        }
    }
}


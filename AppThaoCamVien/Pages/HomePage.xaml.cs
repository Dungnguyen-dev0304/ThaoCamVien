using AppThaoCamVien.Services;

namespace AppThaoCamVien.Pages;

public partial class HomePage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly IServiceProvider _sp;
    private int _langIdx = 0;

    public HomePage(DatabaseService db, IServiceProvider sp)
    {
        InitializeComponent();
        _db = db;
        _sp = sp;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Đồng bộ nút ngôn ngữ
        var cur = LanguageManager.Current;
        _langIdx = LanguageManager.Languages.FindIndex(l => l.Code == cur);
        if (_langIdx < 0) _langIdx = 0;
        UpdateLangBtn();

        // Sync data ngầm
        _ = Task.Run(() => _db.SyncDataFromApiAsync());
    }

    // ── Đổi ngôn ngữ (xoay vòng 6 ngôn ngữ) ────────────────────────────
    private void OnLangTapped(object sender, TappedEventArgs e)
    {
        _langIdx = (_langIdx + 1) % LanguageManager.Languages.Count;
        var (code, _, _) = LanguageManager.Languages[_langIdx];

        _db.CurrentLanguage = code;
        LanguageManager.Apply(code);
        UpdateLangBtn();
    }

    private void UpdateLangBtn()
    {
        var (_, flag, label) = LanguageManager.Languages[_langIdx];
        LangLbl.Text = $"{flag} {label}";
    }

    // ── Nút Bắt đầu → mở MapPage ────────────────────────────────────────
    private async void OnStartTapped(object sender, TappedEventArgs e)
    {
        var page = _sp.GetService<MapPage>();
        if (page != null) await Navigation.PushAsync(page);
    }

    // ── Tour cards → mở StoryAudioPage ──────────────────────────────────
    private async void OnTour1Clicked(object sender, EventArgs e) => await OpenPoi(11); // Khu voi
    private async void OnTour2Clicked(object sender, EventArgs e) => await OpenPoi(4);  // Hà mã (demo)
    private async void OnTour3Clicked(object sender, EventArgs e) => await OpenPoi(8);  // Hươu cao cổ

    private async Task OpenPoi(int poiId)
    {
        try
        {
            var poi = await _db.GetPoiByIdAsync(poiId);
            if (poi == null)
            {
                await DisplayAlert("Thông báo", "Không tìm thấy điểm tham quan.", "OK");
                return;
            }
            var page = _sp.GetService<StoryAudioPage>();
            if (page != null)
            {
                page.LoadPoi(poi);
                await Navigation.PushAsync(page);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }
}

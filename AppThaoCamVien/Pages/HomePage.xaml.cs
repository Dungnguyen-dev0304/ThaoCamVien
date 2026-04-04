using AppThaoCamVien.Services;

namespace AppThaoCamVien.Pages;

public partial class HomePage : ContentPage
{
    private readonly DatabaseService _databaseService;
    private readonly IServiceProvider _serviceProvider;

    // Vị trí hiện tại trong danh sách ngôn ngữ hỗ trợ
    private int _langIndex = 0;

    public HomePage(DatabaseService databaseService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _serviceProvider = serviceProvider;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Đồng bộ nút ngôn ngữ với trạng thái hiện tại
        var currentLang = LanguageManager.GetCurrentLanguage();
        _langIndex = LanguageManager.SupportedLanguages
            .FindIndex(l => l.Code == currentLang);
        if (_langIndex < 0) _langIndex = 0;
        UpdateLangButton();

        // Kéo dữ liệu mới nhất về DB chạy ngầm (không block UI)
        _ = Task.Run(async () => await _databaseService.SyncDataFromApiAsync());
    }

    // ===== Chuyển ngôn ngữ =====
    // Mỗi lần nhấn sẽ xoay vòng qua các ngôn ngữ: VI → EN → TH → ID → MS → KM → VI
    private void OnLanguageTapped(object sender, TappedEventArgs e)
    {
        _langIndex = (_langIndex + 1) % LanguageManager.SupportedLanguages.Count;
        var (code, _, _) = LanguageManager.SupportedLanguages[_langIndex];

        // Lưu vào DatabaseService để AudioService / NarrationEngine biết dùng ngôn ngữ nào
        _databaseService.CurrentLanguage = code;

        // Gọi LanguageManager để cập nhật TẤT CẢ DynamicResource trong app ngay lập tức
        LanguageManager.SetLanguage(code);

        // Cập nhật nhãn nút
        UpdateLangButton();
    }

    private void UpdateLangButton()
    {
        var (_, flag, label) = LanguageManager.SupportedLanguages[_langIndex];
        LangLabel.Text = $"{flag} {label}";
    }

    // ===== Nhấn "Bắt đầu chuyến đi" =====
    private async void OnStartTourTapped(object sender, TappedEventArgs e)
    {
        // Mở trang bản đồ (cần lấy qua ServiceProvider vì MapPage có DI phức tạp)
        var mapPage = _serviceProvider.GetService<MapPage>();
        if (mapPage != null)
            await Navigation.PushAsync(mapPage);
    }

    // ===== Tour Cards =====
    // Những nút này sẽ mở POI cụ thể (PoiId cứng theo seed data)
    private async void OnTour1Clicked(object sender, EventArgs e)
        => await OpenPoiById(2); // Voi Châu Á

    private async void OnTour2Clicked(object sender, EventArgs e)
        => await OpenPoiById(1); // Hổ Đông Dương

    private async void OnTour3Clicked(object sender, EventArgs e)
        => await OpenPoiById(3); // Hươu Cao Cổ

    private async Task OpenPoiById(int poiId)
    {
        try
        {
            var poi = await _databaseService.GetPoiByIdAsync(poiId);
            if (poi == null)
            {
                await DisplayAlert("Thông báo", "Không tìm thấy thông tin điểm tham quan.", "OK");
                return;
            }

            var storyPage = _serviceProvider.GetService<StoryAudioPage>();
            if (storyPage != null)
            {
                storyPage.LoadPoi(poi);
                await Navigation.PushAsync(storyPage);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }
}

using AppThaoCamVien.Services;

namespace AppThaoCamVien.Pages;

public partial class HomePage : ContentPage
{
    private readonly DatabaseService _databaseService;

    public HomePage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Cập nhật lại UI hiển thị ngôn ngữ
        LangLabel.Text = _databaseService.CurrentLanguage == "vi" ? "🇻🇳 VI" : "🇬🇧 EN";

        // Kéo dữ liệu chạy ngầm
        _ = Task.Run(async () => await _databaseService.SyncDataFromApiAsync());
    }

    // Sự kiện đổi ngôn ngữ
    private void OnLanguageTapped(object sender, TappedEventArgs e)
    {
        string newLang = _databaseService.CurrentLanguage == "vi" ? "en" : "vi";

        // Lưu vào DatabaseService (để dùng cho Audio/TTS)
        _databaseService.CurrentLanguage = newLang;

        // Lưu vào Preferences (để lần sau mở app vẫn giữ nguyên)
        Preferences.Set("AppLang", newLang);

        // Kích hoạt đổi giao diện
        LanguageManager.SetLanguage(newLang);
        LangLabel.Text = newLang == "vi" ? "🇻🇳 VI" : "🇬🇧 EN";

        // Khởi động lại Shell để cập nhật triệt để thanh Tab ở dưới đáy
        Application.Current.MainPage = new AppShell();
    }

    private async void OnOpenMapClicked(object sender, EventArgs e)
    {
        var mapPage = Handler?.MauiContext?.Services.GetService<AppThaoCamVien.Pages.MapPage>();
        if (mapPage != null) await Navigation.PushAsync(mapPage);
    }
}
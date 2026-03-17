namespace AppThaoCamVien.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
    }

    // Sự kiện mở trang Bản đồ MAUI
    private async void OnOpenMapClicked(object sender, EventArgs e)
    {
        // Nhờ hệ thống DI (Dependency Injection) tự động khởi tạo MapPage
        // Nó sẽ tự động "bơm" đủ cả 3 service (Database, Location, Geofencing) vào trang này.
        var mapPage = IPlatformApplication.Current.Services.GetService<AppThaoCamVien.Pages.MapPage>();

        // Mở trang
        await Navigation.PushAsync(mapPage);
    }
}
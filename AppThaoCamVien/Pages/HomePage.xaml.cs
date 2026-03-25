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
        try
        {
            var mapPage = IPlatformApplication.Current.Services.GetService<AppThaoCamVien.Pages.MapPage>();

            if (mapPage == null)
            {
                await DisplayAlert("Lỗi", "Không thể mở bản đồ. Vui lòng thử lại.", "OK");
                return;
            }

            await Navigation.PushAsync(mapPage);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }
}
namespace AppThaoCamVien.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
    }

    // Sự kiện mở trang Bản đồ MAUI
    private async void OnMapClicked(object sender, EventArgs e)
    {
        // Dùng PushModalAsync để bản đồ mở lên full màn hình đè lên trang chủ
        await Navigation.PushModalAsync(new MapPage());
    }
}
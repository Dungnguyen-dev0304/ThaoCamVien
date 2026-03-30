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
            // Hiệu ứng UX khi nhấn
            if (sender is View view)
            {
                await view.FadeTo(0.5, 100);
                await view.FadeTo(1, 100);
            }

            // Cách chuẩn trong MAUI để lấy Page đã được đăng ký cùng với các Services của nó
            var mapPage = Handler?.MauiContext?.Services.GetService<AppThaoCamVien.Pages.MapPage>();

            if (mapPage != null)
            {
                await Navigation.PushAsync(mapPage);
            }
            else
            {
                // Nếu null, tức là bạn chưa đăng ký MapPage trong file MauiProgram.cs
                await DisplayAlert("Lỗi", "Không thể tải Bản đồ. Hãy đảm bảo MapPage đã được đăng ký trong MauiProgram.cs (builder.Services.AddTransient<MapPage>();)", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", ex.Message, "OK");
        }
    }
}
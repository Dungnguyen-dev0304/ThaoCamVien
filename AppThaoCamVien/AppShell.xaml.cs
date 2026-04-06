using AppThaoCamVien.Pages;
using AppThaoCamVien.Services;

namespace AppThaoCamVien;

/// <summary>
/// AppShell: Gán từng Page vào Tab bằng ServiceProvider.
///
/// TẠI SAO KHÔNG DÙNG DataTemplate?
/// Shell DataTemplate tạo Page bằng Activator.CreateInstance() — bỏ qua DI container.
/// Nếu constructor có tham số (DatabaseService, IServiceProvider...) → crash ngay lập tức
/// với lỗi "Android.Runtime.JavaProxyThrowable" / "No parameterless constructor".
///
/// GIẢI PHÁP: Tạo Page thủ công qua ServiceProvider, gán vào ShellContent.Content.
/// Dùng Lazy loading: chỉ tạo Page khi Tab được nhấn lần đầu (tiết kiệm memory).
/// </summary>
public partial class AppShell : Shell
{
    private readonly IServiceProvider _sp;

    public AppShell(IServiceProvider sp)
    {
        _sp = sp;
        InitializeComponent();

        // Đăng ký routes cho Navigation.PushAsync()
        Routing.RegisterRoute(nameof(StoryAudioPage), typeof(StoryAudioPage));
        Routing.RegisterRoute(nameof(MapPage), typeof(MapPage));
        Routing.RegisterRoute(nameof(QrPage), typeof(QrPage));
        Routing.RegisterRoute(nameof(NumpadPage), typeof(NumpadPage));
        Routing.RegisterRoute(nameof(AnimalListPage), typeof(AnimalListPage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));

        // Gán Pages vào Tabs ngay khi Shell được tạo
        // ServiceProvider đảm bảo DI constructor được gọi đúng cách
        InitializeTabs();
    }

    private void InitializeTabs()
    {
        try
        {
            // Tab 1: Home
            HomeContent.Content = _sp.GetRequiredService<HomePage>();

            // Tab 2: QR
            QrContent.Content = _sp.GetRequiredService<QrPage>();

            // Tab 3: Numpad
            NumpadContent.Content = _sp.GetRequiredService<NumpadPage>();

            // Tab 4: Animal List (đây là tab hay crash nhất)
            StoryContent.Content = _sp.GetRequiredService<AnimalListPage>();

            // Tab 5: Map
            MapContent.Content = _sp.GetRequiredService<MapPage>();

            // Flyout: About
            AboutContent.Content = _sp.GetRequiredService<AboutPage>();

            System.Diagnostics.Debug.WriteLine("[AppShell] Tất cả tabs initialized thành công");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] CRITICAL ERROR: {ex}");
            // Hiển thị trang lỗi thay vì crash toàn bộ app
            HomeContent.Content = new ContentPage
            {
                BackgroundColor = Color.FromArgb("#061F14"),
                Content = new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    Spacing = 16,
                    Children =
                    {
                        new Label
                        {
                            Text = "⚠️ Lỗi khởi động",
                            TextColor = Colors.White,
                            FontSize = 20,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = ex.Message,
                            TextColor = Color.FromArgb("#A0C8B4"),
                            FontSize = 13,
                            HorizontalOptions = LayoutOptions.Center,
                            HorizontalTextAlignment = TextAlignment.Center,
                            Margin = new Thickness(20, 0)
                        }
                    }
                }
            };
        }
    }
}

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

        try
        {
            _sp.GetRequiredService<AppPresenceService>().Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] AppPresenceService: {ex.Message}");
        }

        // Đăng ký routes cho Navigation.PushAsync()
        Routing.RegisterRoute(nameof(StoryAudioPage), typeof(StoryAudioPage));
        Routing.RegisterRoute(nameof(MapPage), typeof(MapPage));
        Routing.RegisterRoute(nameof(QrPage), typeof(QrPage));
        Routing.RegisterRoute(nameof(NumpadPage), typeof(NumpadPage));
        Routing.RegisterRoute(nameof(AnimalListPage), typeof(AnimalListPage));
        Routing.RegisterRoute(nameof(AnimalsPage), typeof(AnimalsPage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));

        // Gán Pages vào Tabs ngay khi Shell được tạo
        // ServiceProvider đảm bảo DI constructor được gọi đúng cách
        InitializeTabs();
    }

    private void InitializeTabs()
    {
        // Tách lỗi theo từng tab để tránh 1 tab lỗi làm sập toàn bộ app.
        HomeContent.Content = SafeResolvePage<HomePage>("Home");
        QrContent.Content = SafeResolvePage<QrPage>("QR");
        NumpadContent.Content = SafeResolvePage<NumpadPage>("Numpad");
        StoryContent.Content = SafeResolvePage<AnimalsPage>("Animals");
        MapContent.Content = SafeResolvePage<MapPage>("Map");
        AboutContent.Content = SafeResolvePage<AboutPage>("About");

        System.Diagnostics.Debug.WriteLine("[AppShell] Tabs initialized");
    }

    private Page SafeResolvePage<TPage>(string tabName) where TPage : Page
    {
        try
        {
            return _sp.GetRequiredService<TPage>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Tab '{tabName}' init error: {ex}");
            return new ContentPage
            {
                Title = tabName,
                BackgroundColor = Color.FromArgb("#061F14"),
                Content = new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    Spacing = 12,
                    Children =
                    {
                        new Label
                        {
                            Text = "⚠️ Lỗi tải trang",
                            TextColor = Colors.White,
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 20,
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = $"Tab: {tabName}",
                            TextColor = Color.FromArgb("#A0C8B4"),
                            FontSize = 13,
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = ex.Message,
                            TextColor = Color.FromArgb("#A0C8B4"),
                            FontSize = 12,
                            HorizontalTextAlignment = TextAlignment.Center,
                            Margin = new Thickness(20, 0)
                        }
                    }
                }
            };
        }
    }
}

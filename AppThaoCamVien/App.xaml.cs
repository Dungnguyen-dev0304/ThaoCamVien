using AppThaoCamVien.Pages;
using AppThaoCamVien.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace AppThaoCamVien;

public partial class App : Application
{
    public App(IServiceProvider sp)
    {
        InitializeComponent();
        ConfigureGlobalExceptionGuards();

        LanguageManager.Load();

        // Nếu thiết bị thật chưa config IP → hỏi dev nhập 1 lần, lưu Preferences.
        // Ai clone repo về cũng được hỏi riêng, không cần sửa source.
        

        if (!Preferences.Get(OnboardingNavigationHelper.PrefOnboardingDone, false))
        {
            var welcome = sp.GetRequiredService<OnboardingWelcomePage>();
            var nav = new NavigationPage(welcome)
            {
                BarBackgroundColor = Color.FromArgb("#1B5E3A"),
                BarTextColor = Colors.White
            };
            MainPage = nav;
        }
        else
        {
            MainPage = new AppShell(sp);
        }
    }

    /// <summary>
    /// Hỏi dev nhập IP máy chạy API. Chỉ hiện 1 lần trên thiết bị thật.
    /// Ví dụ: dev chạy `ipconfig` thấy 192.168.1.5 → nhập "192.168.1.5".
    /// Lưu vào Preferences, lần sau không hỏi nữa.
    /// </summary>
    private static void ConfigureGlobalExceptionGuards()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FATAL] {e.ExceptionObject}");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[UNOBSERVED] {e.Exception}");
                e.SetObserved();
            }
            catch { }
        };
    }
}

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

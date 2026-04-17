using AppThaoCamVien.Pages;
using AppThaoCamVien.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace AppThaoCamVien;

public partial class App : Application
{
    private readonly IServiceProvider _sp;

    public App(IServiceProvider sp)
    {
        _sp = sp;
        InitializeComponent();
        ConfigureGlobalExceptionGuards();
        LanguageManager.Load();
    }

    /// <summary>
    /// MAUI 10: CreateWindow thay th? MainPage setter (?ã obsolete).
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page root;
        if (!Preferences.Get(OnboardingNavigationHelper.PrefOnboardingDone, false))
        {
            var welcome = _sp.GetRequiredService<OnboardingWelcomePage>();
            root = new NavigationPage(welcome)
            {
                BarBackgroundColor = Color.FromArgb("#1B5E3A"),
                BarTextColor = Colors.White
            };
        }
        else
        {
            var mustConfigureApi =
#if ANDROID
                DeviceInfo.DeviceType == DeviceType.Physical && ApiService.NeedsConfiguration;
#else
                ApiService.NeedsConfiguration;
#endif
            if (mustConfigureApi)
            {
                var apiConfig = _sp.GetRequiredService<OnboardingApiConfigPage>();
                root = new NavigationPage(apiConfig)
                {
                    BarBackgroundColor = Color.FromArgb("#1B5E3A"),
                    BarTextColor = Colors.White
                };
            }
            else
            {
                root = new AppShell(_sp);
            }
        }

        return new Window(root);
    }

    /// <summary>
    /// Global Exception Guards — b?t T?T C? unhandled exceptions.
    ///
    /// 3 t?ng b?o v?:
    ///   1. AppDomain.UnhandledException — crash c?p process
    ///   2. TaskScheduler.UnobservedTaskException — async void / fire-and-forget
    ///   3. MauiExceptions.UnhandledException — MAUI-specific (UI thread crashes)
    ///
    /// M?c ?ích: KHÔNG BAO GI? ?? app crash tr?ng màn hình.
    /// Thay vào ?ó: log l?i và hi?n th? alert thân thi?n.
    /// </summary>
    private static void ConfigureGlobalExceptionGuards()
    {
        // ?? T?ng 1: Process-level crashes ???????????????????????????????
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"[FATAL] AppDomain: {ex?.Message ?? e.ExceptionObject}");
                LogCrash("AppDomain", ex);
            }
            catch { /* Không throw trong exception handler */ }
        };

        // ?? T?ng 2: Fire-and-forget task exceptions ?????????????????????
        // Khi dùng `_ = SomeAsyncMethod()` mà method throw,
        // exception s? b? nu?t ? app ch?y sai tr?ng thái.
        // SetObserved() ng?n runtime ném l?i exception khi GC finalize task.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[UNOBSERVED] {e.Exception}");
                LogCrash("UnobservedTask", e.Exception);
                e.SetObserved(); // Ng?n crash khi GC collect
            }
            catch { }
        };

        // ?? T?ng 3: MAUI UI thread exceptions ??????????????????????????
        // MauiExceptions ?ã b? lo?i b? trong .NET MAUI 10.
        // Android: ?ã có AndroidEnvironment.UnhandledExceptionRaiser trong MainActivity.cs.
        // Dùng FirstChanceException ?? log crash s?m nh?t (t?t c? platform).
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            // Ch? log, KHÔNG swallow — ?? exception propagate bình th??ng.
            // M?c ?ích: debug log cho crash khó tái hi?n.
            try
            {
                var ex = e.Exception;
                if (ex is TaskCanceledException or OperationCanceledException)
                    return; // B? qua cancel — quá nhi?u noise

                System.Diagnostics.Debug.WriteLine(
                    $"[FIRST-CHANCE] {ex.GetType().Name}: {Truncate(ex.Message, 120)}");
            }
            catch { }
        };
    }

    /// <summary>
    /// Ghi crash log vào file local ?? debug sau.
    /// File: {AppData}/crash_log.txt (append mode).
    /// </summary>
    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var logPath = Path.Combine(FileSystem.AppDataDirectory, "crash_log.txt");
            var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] " +
                        $"{ex?.GetType().Name}: {ex?.Message}\n" +
                        $"  StackTrace: {Truncate(ex?.StackTrace, 500)}\n\n";
            File.AppendAllText(logPath, entry);
        }
        catch { /* File I/O fail ? b? qua */ }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "(null)";
        return s.Length <= max ? s : s[..max] + "…";
    }
}
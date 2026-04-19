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

        // Xoá preference URL server cũ (từ lần test trước) nếu không khớp
        // AppConfig.LanIp / emulator hiện tại — phải chạy TRƯỚC khi bất kỳ
        // service nào (ApiService, DatabaseService…) đọc Preferences.
        AppConfig.EnsureFreshPreferences();

        LanguageManager.Load();
    }

    /// <summary>
    /// MAUI 10: CreateWindow thay th? MainPage setter (?� obsolete).
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
    /// Global Exception Guards � b?t T?T C? unhandled exceptions.
    ///
    /// 3 t?ng b?o v?:
    ///   1. AppDomain.UnhandledException � crash c?p process
    ///   2. TaskScheduler.UnobservedTaskException � async void / fire-and-forget
    ///   3. MauiExceptions.UnhandledException � MAUI-specific (UI thread crashes)
    ///
    /// M?c ?�ch: KH�NG BAO GI? ?? app crash tr?ng m�n h�nh.
    /// Thay v�o ?�: log l?i v� hi?n th? alert th�n thi?n.
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
            catch { /* Kh�ng throw trong exception handler */ }
        };

        // ?? T?ng 2: Fire-and-forget task exceptions ?????????????????????
        // Khi d�ng `_ = SomeAsyncMethod()` m� method throw,
        // exception s? b? nu?t ? app ch?y sai tr?ng th�i.
        // SetObserved() ng?n runtime n�m l?i exception khi GC finalize task.
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
        // MauiExceptions ?� b? lo?i b? trong .NET MAUI 10.
        // Android: ?� c� AndroidEnvironment.UnhandledExceptionRaiser trong MainActivity.cs.
        // D�ng FirstChanceException ?? log crash s?m nh?t (t?t c? platform).
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            // Ch? log, KH�NG swallow � ?? exception propagate b�nh th??ng.
            // M?c ?�ch: debug log cho crash kh� t�i hi?n.
            try
            {
                var ex = e.Exception;
                if (ex is TaskCanceledException or OperationCanceledException)
                    return; // B? qua cancel � qu� nhi?u noise

                System.Diagnostics.Debug.WriteLine(
                    $"[FIRST-CHANCE] {ex.GetType().Name}: {Truncate(ex.Message, 120)}");
            }
            catch { }
        };
    }

    /// <summary>
    /// Ghi crash log v�o file local ?? debug sau.
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
        return s.Length <= max ? s : s[..max] + "�";
    }
}
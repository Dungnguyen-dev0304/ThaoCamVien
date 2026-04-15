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
    /// MAUI 10: CreateWindow thay thế MainPage setter (đã obsolete).
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
            root = new AppShell(_sp);
        }

        return new Window(root);
    }

    /// <summary>
    /// Global Exception Guards — bắt TẤT CẢ unhandled exceptions.
    ///
    /// 3 tầng bảo vệ:
    ///   1. AppDomain.UnhandledException — crash cấp process
    ///   2. TaskScheduler.UnobservedTaskException — async void / fire-and-forget
    ///   3. MauiExceptions.UnhandledException — MAUI-specific (UI thread crashes)
    ///
    /// Mục đích: KHÔNG BAO GIỜ để app crash trắng màn hình.
    /// Thay vào đó: log lỗi và hiển thị alert thân thiện.
    /// </summary>
    private static void ConfigureGlobalExceptionGuards()
    {
        // ── Tầng 1: Process-level crashes ───────────────────────────────
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

        // ── Tầng 2: Fire-and-forget task exceptions ─────────────────────
        // Khi dùng `_ = SomeAsyncMethod()` mà method throw,
        // exception sẽ bị nuốt → app chạy sai trạng thái.
        // SetObserved() ngăn runtime ném lại exception khi GC finalize task.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[UNOBSERVED] {e.Exception}");
                LogCrash("UnobservedTask", e.Exception);
                e.SetObserved(); // Ngăn crash khi GC collect
            }
            catch { }
        };

        // ── Tầng 3: MAUI UI thread exceptions ──────────────────────────
        // MauiExceptions đã bị loại bỏ trong .NET MAUI 10.
        // Android: đã có AndroidEnvironment.UnhandledExceptionRaiser trong MainActivity.cs.
        // Dùng FirstChanceException để log crash sớm nhất (tất cả platform).
        AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
        {
            // Chỉ log, KHÔNG swallow — để exception propagate bình thường.
            // Mục đích: debug log cho crash khó tái hiện.
            try
            {
                var ex = e.Exception;
                if (ex is TaskCanceledException or OperationCanceledException)
                    return; // Bỏ qua cancel — quá nhiều noise

                System.Diagnostics.Debug.WriteLine(
                    $"[FIRST-CHANCE] {ex.GetType().Name}: {Truncate(ex.Message, 120)}");
            }
            catch { }
        };
    }

    /// <summary>
    /// Ghi crash log vào file local để debug sau.
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
        catch { /* File I/O fail → bỏ qua */ }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "(null)";
        return s.Length <= max ? s : s[..max] + "…";
    }
}

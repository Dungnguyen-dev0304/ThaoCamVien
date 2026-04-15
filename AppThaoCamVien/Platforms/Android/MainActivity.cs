using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace AppThaoCamVien
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation
            | ConfigChanges.UiMode | ConfigChanges.ScreenLayout
            | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Android-specific: bắt Java unhandled exceptions
            // Đây là tầng cuối cùng trước khi Android kill process.
            AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;
        }

        private void OnAndroidUnhandledException(object? sender, RaiseThrowableEventArgs e)
        {
            try
            {
                var ex = e.Exception;
                System.Diagnostics.Debug.WriteLine($"[ANDROID-FATAL] {ex?.GetType().Name}: {ex?.Message}");

                // Ghi crash log
                var logPath = System.IO.Path.Combine(
                    FileSystem.AppDataDirectory, "crash_log.txt");
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [AndroidFatal] " +
                            $"{ex?.GetType().Name}: {ex?.Message}\n" +
                            $"  Stack: {ex?.StackTrace?[..Math.Min(ex.StackTrace.Length, 500)]}\n\n";
                System.IO.File.AppendAllText(logPath, entry);
            }
            catch { }

            // e.Handled = true → ngăn crash (NGUY HIỂM: app có thể ở trạng thái hỏng)
            // Chỉ set true cho các lỗi non-critical.
            // Mặc định: để false → Android sẽ hiển thị "App has stopped".
            e.Handled = false;
        }

        protected override void OnDestroy()
        {
            AndroidEnvironment.UnhandledExceptionRaiser -= OnAndroidUnhandledException;
            base.OnDestroy();
        }
    }
}

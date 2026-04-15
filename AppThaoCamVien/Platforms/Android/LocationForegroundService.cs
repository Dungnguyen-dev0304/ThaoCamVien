// Platforms/Android/LocationForegroundService.cs
#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

#pragma warning disable CA1416 // Platform compatibility — đã có runtime check Build.VERSION.SdkInt
namespace AppThaoCamVien.Platforms.Android
{
    /// <summary>
    /// Android Foreground Service — đảm bảo GPS tiếp tục chạy
    /// ngay cả khi người dùng minimize app hoặc tắt màn hình.
    /// Yêu cầu Android 8+ (API 26+): phải gọi StartForeground() trong 5 giây.
    /// </summary>
    [Service(ForegroundServiceType = ForegroundService.TypeLocation,
             Exported = false)]
    public class LocationForegroundService : Service
    {
        private const string CHANNEL_ID = "tcv_gps_channel";
        private const int NOTIF_ID = 9001;

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(
            Intent? intent, StartCommandFlags flags, int startId)
        {
            if (intent?.Action == "START")
            {
                CreateChannel();
                StartForeground(NOTIF_ID, BuildNotif("Đang theo dõi vị trí trong Thảo Cầm Viên"));
                System.Diagnostics.Debug.WriteLine("[FgService] Started");
            }
            else if (intent?.Action == "STOP")
            {
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                System.Diagnostics.Debug.WriteLine("[FgService] Stopped");
            }
            return StartCommandResult.Sticky;
        }

        private void CreateChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
            var ch = new NotificationChannel(
                CHANNEL_ID, "Thảo Cầm Viên GPS", NotificationImportance.Low)
            {
                Description = "Theo dõi vị trí để kích hoạt thuyết minh tự động"
            };
            ((NotificationManager?)GetSystemService(NotificationService))
                ?.CreateNotificationChannel(ch);
        }

        private Notification BuildNotif(string text)
        {
            // Tap notification → mở lại app
            var launch = global::Android.App.Application.Context
                .PackageManager
                ?.GetLaunchIntentForPackage(
                    global::Android.App.Application.Context.PackageName ?? "");
            launch?.SetFlags(ActivityFlags.SingleTop);

            var pi = PendingIntent.GetActivity(
                this, 0, launch,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            return new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetSmallIcon(AppThaoCamVien.Resource.Drawable.logothaocamvien)
                .SetContentTitle("🦁 Thảo Cầm Viên Audio Tour")
                .SetContentText(text)
                .SetOngoing(true)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetContentIntent(pi)
                .SetVisibility(NotificationCompat.VisibilityPublic)
                .Build();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            System.Diagnostics.Debug.WriteLine("[FgService] Destroyed");
        }
    }
}
#endif

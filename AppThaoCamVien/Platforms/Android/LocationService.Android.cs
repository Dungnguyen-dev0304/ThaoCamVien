using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

#pragma warning disable CA1416 // Platform compatibility — đã có runtime check Build.VERSION.SdkInt
namespace AppThaoCamVien.Platforms.Android
{
    [Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation)]
    public class AndroidLocationService : Service
    {
        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            var channelId = "LocationServiceChannel";

            // Tạo Notification Channel (Bắt buộc từ Android 8+)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channel = new NotificationChannel(channelId, "GPS Tracking", NotificationImportance.Low);
                var manager = (NotificationManager?)GetSystemService(NotificationService);
                manager?.CreateNotificationChannel(channel);
            }

            // Tạo thông báo hiển thị trên thanh trạng thái
            var notification = new NotificationCompat.Builder(this, channelId)
                .SetContentTitle("Thảo Cầm Viên Tour")
                .SetContentText("Đang theo dõi vị trí để tự động phát thuyết minh...")
                .SetSmallIcon(AppThaoCamVien.Resource.Drawable.logothaocamvien) // Đổi thành icon app của bạn
                .SetOngoing(true)
                .Build();

            // Kích hoạt Foreground Service
            StartForeground(1001, notification);

            return StartCommandResult.Sticky;
        }
    }
}
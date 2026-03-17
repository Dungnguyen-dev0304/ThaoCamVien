using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using AppThaoCamVien.Services;

namespace AppThaoCamVien
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiMaps()
                .UseSkiaSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            builder.Services.AddSingleton<DatabaseService>();

            // Đăng ký MapPage để nó có thể nhận DatabaseService
            builder.Services.AddTransient<Pages.MapPage>();
            // ĐĂNG KÝ SERVICES (Giữ nguyên cái cũ, thêm 2 cái mới)
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<LocationService>();   // <-- THÊM MỚI
            builder.Services.AddSingleton<GeofencingEngine>();  // <-- THÊM MỚI

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using AppThaoCamVien.Services;
using AppThaoCamVien.Pages;
using ZXing.Net.Maui.Controls;
using CommunityToolkit.Maui;
using Plugin.Maui.Audio;

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
                .UseBarcodeReader()           // ZXing QR scanner
                .UseMauiCommunityToolkit()    // CommunityToolkit helpers
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // === Plugin Audio ===
            builder.Services.AddSingleton(AudioManager.Current);

            // === SERVICES ===
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<LocationService>();
            builder.Services.AddSingleton<GeofencingEngine>();
            builder.Services.AddSingleton<AudioService>();

            // === PAGES (Transient = tạo mới mỗi lần navigate) ===
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<MapPage>();
            builder.Services.AddTransient<QrPage>();
            builder.Services.AddTransient<NumpadPage>();
            builder.Services.AddTransient<StoryAudioPage>();
            builder.Services.AddTransient<AboutPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

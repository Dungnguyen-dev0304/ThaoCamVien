#if ANDROID
using Android.Speech.Tts;
#endif
using AppThaoCamVien.Pages;
using AppThaoCamVien.Services;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using SkiaSharp.Views.Maui.Controls.Hosting;
using ZXing.Net.Maui.Controls;

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
                .UseBarcodeReader()
                .UseMauiCommunityToolkit()
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
            builder.Services.AddSingleton<TtsEngine>();          // ← MỚI: engine TTS đa ngôn ngữ
            builder.Services.AddSingleton<NarrationEngine>();    // ← đã cập nhật nhận TtsEngine
            builder.Services.AddSingleton<AutoTranslateService>();

            // === PAGES ===
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<MapPage>();
            builder.Services.AddTransient<QrPage>();
            builder.Services.AddTransient<NumpadPage>();
            builder.Services.AddTransient<StoryAudioPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<AnimalListPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

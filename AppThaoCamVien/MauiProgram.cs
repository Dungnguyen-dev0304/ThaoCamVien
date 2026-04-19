using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using AppThaoCamVien.Services;
using AppThaoCamVien.Pages;
using ZXing.Net.Maui.Controls;
using CommunityToolkit.Maui;
using Plugin.Maui.Audio;
using AppThaoCamVien.ViewModels;

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

            // Audio plugin
            builder.Services.AddSingleton(AudioManager.Current);

            // ── Services (Singleton) ──────────────────────────────────────
            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<LocationService>();
            builder.Services.AddSingleton<GeofencingEngine>();
            builder.Services.AddSingleton<AudioService>();
            builder.Services.AddSingleton<TtsEngine>();
            builder.Services.AddSingleton<NarrationEngine>();
            builder.Services.AddSingleton<AutoTranslateService>();
            builder.Services.AddSingleton<ApiService>();
            builder.Services.AddSingleton<AppPresenceService>();
            builder.Services.AddSingleton<DirectionsService>();

            // ── Pages ─────────────────────────────────────────────────────
            // QUAN TRỌNG: Dùng Transient cho tất cả Pages.
            // Shell sẽ lấy Page từ ServiceProvider, KHÔNG tạo qua DataTemplate.
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<MapPage>();
            builder.Services.AddTransient<QrPage>();
            builder.Services.AddTransient<NumpadPage>();
            builder.Services.AddTransient<StoryAudioPage>();
            builder.Services.AddTransient<AboutPage>();
            builder.Services.AddTransient<AnimalListPage>();
            builder.Services.AddTransient<AnimalsPage>();
            builder.Services.AddTransient<SettingsPage>();

            builder.Services.AddSingleton<OfflineBundleDownloadService>();
            builder.Services.AddTransient<HomePageViewModel>();
            builder.Services.AddTransient<AboutPageViewModel>();
            builder.Services.AddTransient<MapPageViewModel>();
            builder.Services.AddTransient<QrPageViewModel>();
            builder.Services.AddTransient<StoryAudioViewModel>();
            builder.Services.AddTransient<AnimalsViewModel>();
            builder.Services.AddTransient<OnboardingWelcomePage>();
            builder.Services.AddTransient<OnboardingPermissionsPage>();
            builder.Services.AddTransient<OnboardingApiConfigPage>();
            builder.Services.AddTransient<OnboardingOfflineDownloadPage>();
#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

            // IServiceProvider được tự động inject bởi MAUI DI container
            // Không cần đă
namespace AppThaoCamVien;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(Pages.MapPage), typeof(Pages.MapPage));
        Routing.RegisterRoute(nameof(Pages.QrPage), typeof(Pages.QrPage));
        Routing.RegisterRoute(nameof(Pages.NumpadPage), typeof(Pages.NumpadPage));
        Routing.RegisterRoute(nameof(Pages.StoryAudioPage), typeof(Pages.StoryAudioPage));
        Routing.RegisterRoute(nameof(Pages.AnimalListPage), typeof(Pages.AnimalListPage));
    }
}
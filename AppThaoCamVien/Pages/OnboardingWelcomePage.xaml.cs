namespace AppThaoCamVien.Pages;

public partial class OnboardingWelcomePage : ContentPage
{
    private readonly IServiceProvider _sp;

    public OnboardingWelcomePage(IServiceProvider sp)
    {
        _sp = sp;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        NavigationPage.SetHasNavigationBar(this, false);
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(_sp.GetRequiredService<OnboardingPermissionsPage>());
    }
}

using AppThaoCamVien.Services;
using Microsoft.Maui.ApplicationModel;

namespace AppThaoCamVien.Pages;

public partial class OnboardingPermissionsPage : ContentPage
{
    private readonly IServiceProvider _sp;
    private readonly LocationService _locationService;

    public OnboardingPermissionsPage(IServiceProvider sp, LocationService locationService)
    {
        _sp = sp;
        _locationService = locationService;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        NavigationPage.SetHasNavigationBar(this, true);
    }

    private async void OnRequestLocationClicked(object? sender, EventArgs e)
    {
        var ok = await _locationService.CheckAndRequestPermissionAsync();
        if (!ok)
            HintLabel.Text = Application.Current?.Resources["OnboardingPermSkipped"] as string ?? "";
        HintLabel.IsVisible = true;
    }

    private async void OnRequestCameraClicked(object? sender, EventArgs e)
    {
        var s = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (s != PermissionStatus.Granted)
            s = await Permissions.RequestAsync<Permissions.Camera>();
        HintLabel.IsVisible = true;
    }

    private async void OnRequestNotificationsClicked(object? sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var s = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (s != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.PostNotifications>();
        }
        catch
        {
            // API không hỗ trợ trên một số phiên bản — bỏ qua
        }
#endif
        HintLabel.IsVisible = true;
    }

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(_sp.GetRequiredService<OnboardingOfflineDownloadPage>());
    }
}

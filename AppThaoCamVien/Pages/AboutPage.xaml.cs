using AppThaoCamVien.ViewModels;

namespace AppThaoCamVien.Pages;

public partial class AboutPage : ContentPage
{
    private readonly AboutPageViewModel _vm;

    public AboutPage(AboutPageViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.SafeReloadAsync();
    }
}
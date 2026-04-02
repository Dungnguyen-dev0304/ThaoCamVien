using AppThaoCamVien.Services;

namespace AppThaoCamVien.Pages;

public partial class HomePage : ContentPage
{
    private readonly DatabaseService _databaseService;

    // Sửa lại constructor để nhận DatabaseService
    public HomePage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Chạy ngầm việc đồng bộ dữ liệu từ API về máy để dùng offline
        // Không dùng 'await' ở đây để giao diện không bị đơ, cho nó chạy nền
        _ = Task.Run(async () =>
        {
            await _databaseService.SyncDataFromApiAsync();
        });
    }

    private async void OnOpenMapClicked(object sender, EventArgs e)
    {
        // Code của bạn giữ nguyên...
    }
}
using AppThaoCamVien.Services;
using Microsoft.Maui.Devices;

namespace AppThaoCamVien.Pages;

/// <summary>
/// Trang Cài đặt — 2 công cụ chính cho demo:
///   1. Tải lại dữ liệu từ server   → sync POI + bảng audio metadata.
///   2. Xoá cache audio              → buộc app tải lại file mới nhất.
/// Kèm theo: hiển thị URL server hiện tại để debug khi đổi máy / đổi IP.
/// </summary>
public partial class SettingsPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly ApiService _api;

    public SettingsPage(DatabaseService db, ApiService api)
    {
        InitializeComponent();
        _db = db;
        _api = api;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Luôn refresh URL trước khi hiển thị, phòng trường hợp dev đổi IP runtime
        _api.RefreshBaseUrlFromPreferences();
        _db.RefreshApiBaseUrl();

        ApiUrlLabel.Text = $"API: {_api.BaseUrl}";
        WebUrlLabel.Text = $"Web: {MediaUrlResolver.WebBaseUrl}";
        DeviceTypeLabel.Text = $"Thiết bị: {DeviceInfo.Current.DeviceType} — {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}";
    }

    // ────────────────────────────────────────────────────────────
    // Hành động
    // ────────────────────────────────────────────────────────────

    private async void OnReloadClicked(object? sender, EventArgs e)
    {
        await RunAsync(
            "Đang tải lại…",
            async () =>
            {
                // Gọi tuần tự: POI trước, rồi API tự gọi SyncMedia cuối hàm.
                await _db.SyncDataFromApiAsync();
                return "✓ Đã đồng bộ POI + audio mới nhất.";
            });
    }

    private async void OnClearCacheClicked(object? sender, EventArgs e)
    {
        var ok = await DisplayAlert(
            "Xoá cache audio?",
            "Toàn bộ file audio đã tải về máy sẽ bị xoá. Lần phát tiếp theo sẽ tải lại từ server.",
            "Xoá",
            "Huỷ");
        if (!ok) return;

        await RunAsync(
            "Đang xoá cache…",
            async () =>
            {
                await Task.Run(AudioService.ClearCache);
                return "✓ Đã xoá cache audio.";
            });
    }

    // ────────────────────────────────────────────────────────────
    // Helper: disable buttons + spinner + status + auto-restore
    // ────────────────────────────────────────────────────────────

    private async Task RunAsync(string busyText, Func<Task<string>> work)
    {
        try
        {
            ReloadButton.IsEnabled = false;
            ClearCacheButton.IsEnabled = false;
            LoadingBar.IsVisible = true;
            LoadingBar.IsRunning = true;
            StatusLabel.Text = busyText;
            StatusLabel.TextColor = Colors.Gray;

            var msg = await work();

            StatusLabel.Text = msg;
            StatusLabel.TextColor = Color.FromArgb("#1B5E3A");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"✗ Lỗi: {ex.Message}";
            StatusLabel.TextColor = Colors.Red;
            System.Diagnostics.Debug.WriteLine($"[Settings] {ex}");
        }
        finally
        {
            LoadingBar.IsVisible = false;
            LoadingBar.IsRunning = false;
            ReloadButton.IsEnabled = true;
            ClearCacheButton.IsEnabled = true;
        }
    }
}

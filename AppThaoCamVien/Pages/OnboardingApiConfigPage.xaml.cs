using AppThaoCamVien.Services;
using Microsoft.Maui.Storage;
using System.Diagnostics;

namespace AppThaoCamVien.Pages;

public partial class OnboardingApiConfigPage : ContentPage
{
    private readonly IServiceProvider _sp;
    private readonly ApiService _api;

    public OnboardingApiConfigPage(IServiceProvider sp, ApiService api)
    {
        _sp = sp;
        _api = api;
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        NavigationPage.SetHasNavigationBar(this, true);

        // Pre-fill nếu đã có IP lưu trước đó
        var saved = Preferences.Default.Get("ApiBaseUrl", string.Empty);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            // Parse ra phần IP từ URL đã lưu
            var ip = ExtractIpFromUrl(saved);
            if (!string.IsNullOrEmpty(ip))
                IpEntry.Text = ip;
        }
    }

    private async void OnTestConnectionClicked(object? sender, EventArgs e)
    {
        var input = (IpEntry.Text ?? "").Trim();
        if (string.IsNullOrEmpty(input))
        {
            ShowStatus("Vui lòng nhập địa chỉ IP.", isError: true);
            return;
        }

        var baseUrl = BuildUrl(input);

        TestBtn.IsEnabled = false;
        TestSpinner.IsVisible = true;
        TestSpinner.IsRunning = true;
        ShowStatus("Đang kiểm tra kết nối...", isError: false);

        try
        {
            using var handler = new HttpClientHandler();
#if DEBUG
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            var testUrl = $"{baseUrl}/api/Pois?lang=vi";
            Debug.WriteLine($"[ApiConfig] Testing: {testUrl}");

            var response = await http.GetAsync(testUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                ShowStatus($"Kết nối thành công! Server phản hồi OK.", isError: false);
                StatusLabel.TextColor = Color.FromArgb("#2E7D32");
            }
            else
            {
                ShowStatus($"Server phản hồi mã {(int)response.StatusCode}. Kiểm tra lại API.", isError: true);
            }
        }
        catch (TaskCanceledException)
        {
            ShowStatus("Hết thời gian chờ (timeout). Kiểm tra IP và đảm bảo server đang chạy.", isError: true);
        }
        catch (HttpRequestException ex)
        {
            ShowStatus($"Không kết nối được: {ex.Message}", isError: true);
        }
        catch (Exception ex)
        {
            ShowStatus($"Lỗi: {ex.Message}", isError: true);
        }
        finally
        {
            TestBtn.IsEnabled = true;
            TestSpinner.IsVisible = false;
            TestSpinner.IsRunning = false;
        }
    }

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        var input = (IpEntry.Text ?? "").Trim();
        if (!string.IsNullOrEmpty(input))
        {
            var baseUrl = BuildUrl(input);
            SaveApiUrl(baseUrl);
        }
        // Nếu input rỗng trên emulator → dùng default (10.0.2.2:5281)

        await Navigation.PushAsync(_sp.GetRequiredService<OnboardingOfflineDownloadPage>());
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        // Không lưu gì → dùng default URL (emulator hoặc sentinel)
        await Navigation.PushAsync(_sp.GetRequiredService<OnboardingOfflineDownloadPage>());
    }

    /// <summary>
    /// Lưu URL vào Preferences và cập nhật ApiService + DatabaseService đang chạy.
    /// </summary>
    private void SaveApiUrl(string baseUrl)
    {
        Preferences.Default.Set("ApiBaseUrl", baseUrl);
        Debug.WriteLine($"[ApiConfig] Saved ApiBaseUrl = {baseUrl}");

        // Cập nhật ApiService singleton đang chạy
        _api.BaseUrl = baseUrl;
        _sp.GetService<DatabaseService>()?.RefreshApiBaseUrl();
    }

    /// <summary>
    /// Xây URL từ input người dùng.
    /// Input có thể là: "192.168.1.5", "192.168.1.5:8080", "http://192.168.1.5:5281"
    /// </summary>
    private static string BuildUrl(string input)
    {
        input = input.Trim();

        // Nếu đã có scheme → dùng luôn
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return input.TrimEnd('/');
        }

        // Nếu có port → http://ip:port
        if (input.Contains(':'))
        {
            return $"http://{input}".TrimEnd('/');
        }

        // Chỉ có IP → thêm port mặc định
        return $"http://{input}:5281";
    }

    private static string ExtractIpFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Port == 5281 ? uri.Host : $"{uri.Host}:{uri.Port}";
        }
        catch
        {
            return url.Replace("http://", "").Replace("https://", "").TrimEnd('/');
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = isError ? Color.FromArgb("#D32F2F") : Color.FromArgb("#616161");
        StatusLabel.IsVisible = true;
    }
}

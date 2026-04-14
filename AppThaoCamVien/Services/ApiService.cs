using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

/// <summary>
/// Safe HTTP client wrapper for mobile app.
/// Never throws outside; return null and let ViewModel switch to Error/Empty states.
/// </summary>
public sealed class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService()
    {
        // Cho phép thay BaseUrl nhanh khi test (Android emulator/device, iOS simulator/device).
        // Ví dụ: http://10.0.2.2:5281 hoặc http://192.168.1.100:5281
        // Đọc IP từ Preferences (đã lưu từ lần config trước hoặc từ màn hình cài đặt).
        // Nếu chưa có → dùng default phù hợp với loại thiết bị.
        // Trên thiết bị thật, App.xaml.cs sẽ hỏi dev nhập IP ở lần chạy đầu.
        var pref = Preferences.Default.Get("ApiBaseUrl", string.Empty);
        var defaultUrl = ResolveDefaultApiUrl();
        BaseUrl = string.IsNullOrWhiteSpace(pref) ? defaultUrl : pref;

        var handler = new HttpClientHandler();
#if DEBUG
        // Chỉ áp dụng trong môi trường dev: bypass chứng chỉ self-signed (nếu có).
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12)
        };

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string BaseUrl { get; set; }

    /// <summary>
    /// Chọn URL mặc định theo loại thiết bị. Không hardcode IP cá nhân.
    /// Emulator → 10.0.2.2 (alias localhost của host).
    /// Thiết bị thật → placeholder, sẽ được ghi đè bởi prompt nhập IP ở App.xaml.cs.
    /// </summary>
    internal static string ResolveDefaultApiUrl()
    {
#if ANDROID
        return DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://10.0.2.2:5281"
            : "http://192.168.0.101:5281";  // Sentinel — App.xaml.cs sẽ phát hiện và hỏi dev
#elif IOS
        return DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://localhost:5281"
            : "http://192.168.0.101:5281";
#else
        return "http://localhost:5281";
#endif
    }

    /// <summary>Kiểm tra xem dev đã cấu hình IP chưa.</summary>
    public static bool NeedsConfiguration
        => !Preferences.Default.ContainsKey("ApiBaseUrl")
           && ResolveDefaultApiUrl().Contains("192.168.0.101");

    private string BuildUrl(string endpoint)
        => $"{BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response)
    {
        try { return await response.Content.ReadAsStringAsync(); }
        catch { return null; }
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default) where T : class
    {
        var url = BuildUrl(endpoint);
        try
        {
            return await _httpClient.GetFromJsonAsync<T>(url, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            Debug.WriteLine($"[ApiService] GET timeout: {url}. {ex.Message}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ApiService] GET network error: {url}. {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[ApiService] GET JSON parse error: {url}. {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApiService] GET error: {url}. {ex.Message}");
            return null;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload, CancellationToken ct = default)
        where TResponse : class
    {
        var url = BuildUrl(endpoint);
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(url, payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeReadBodyAsync(response);
                Debug.WriteLine($"[ApiService] POST failed status={(int)response.StatusCode} url={url} body={body}");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            Debug.WriteLine($"[ApiService] POST timeout: {url}. {ex.Message}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[ApiService] POST network error: {url}. {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[ApiService] POST JSON parse error: {url}. {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ApiService] POST error: {url}. {ex.Message}");
            return null;
        }
    }
}

using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Maui.Storage;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace AppThaoCamVien.Services;

/// <summary>
/// Safe HTTP client wrapper for mobile app.
/// Tích hợp Polly v8: Retry (2x exponential) + Circuit Breaker + Timeout.
/// Never throws outside; return null và let ViewModel switch to Error/Empty states.
/// </summary>
public sealed class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline _pipeline;

    public ApiService()
    {
        var pref = Preferences.Default.Get("ApiBaseUrl", string.Empty);
        var defaultUrl = ResolveDefaultApiUrl();
        BaseUrl = string.IsNullOrWhiteSpace(pref) ? defaultUrl : pref;

        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif

        _httpClient = new HttpClient(handler)
        {
            // Timeout của HttpClient > Polly total timeout
            // để Polly quản lý timeout, không phải HttpClient.
            Timeout = TimeSpan.FromSeconds(20)
        };

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Polly pipeline: Timeout(15s) → Retry(2x) → CircuitBreaker
        _pipeline = ResiliencePolicies.HttpPipeline;
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
            : "http://172.20.10.3:5281";  // Sentinel — App.xaml.cs sẽ phát hiện và hỏi dev
#elif IOS
        return DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://localhost:5281"
            : "http://172.20.10.3:5281";
#else
        return "http://localhost:5281";
#endif
    }

    /// <summary>Kiểm tra xem dev đã cấu hình IP chưa.</summary>
    public static bool NeedsConfiguration
        => !Preferences.Default.ContainsKey("ApiBaseUrl")
           && ResolveDefaultApiUrl().Contains("172.20.10.3");

    private string BuildUrl(string endpoint)
        => $"{BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response)
    {
        try { return await response.Content.ReadAsStringAsync(); }
        catch { return null; }
    }

    /// <summary>
    /// GET request với Polly resilience pipeline.
    /// Circuit Breaker mở → fail-fast (không chờ timeout).
    /// Retry → exponential backoff 1s, 2s.
    /// Timeout tổng → 15s cho cả pipeline.
    /// </summary>
    /// <summary>
    /// Gắn Accept-Language header trước mỗi request.
    /// Dùng LanguageManager.Current để lấy ngôn ngữ hiện tại.
    /// Format: "en", "vi", "th"... — server sẽ ưu tiên header này hơn query param.
    /// </summary>
    private void ApplyLanguageHeader()
    {
        var lang = LanguageManager.Current; // "vi", "en", "th", ...
        _httpClient.DefaultRequestHeaders.AcceptLanguage.Clear();
        _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(
            new System.Net.Http.Headers.StringWithQualityHeaderValue(lang));
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default) where T : class
    {
        var url = BuildUrl(endpoint);
        try
        {
            ApplyLanguageHeader();
            return await _pipeline.ExecuteAsync(async token =>
            {
                return await _httpClient.GetFromJsonAsync<T>(url, token);
            }, ct);
        }
        catch (BrokenCircuitException)
        {
            Debug.WriteLine($"[ApiService] GET circuit open (fail-fast): {url}");
            return null;
        }
        catch (TimeoutRejectedException)
        {
            Debug.WriteLine($"[ApiService] GET total timeout: {url}");
            return null;
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

    /// <summary>
    /// POST request với Polly resilience pipeline.
    /// </summary>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload, CancellationToken ct = default)
        where TResponse : class
    {
        var url = BuildUrl(endpoint);
        try
        {
            ApplyLanguageHeader();
            return await _pipeline.ExecuteAsync(async token =>
            {
                using var response = await _httpClient.PostAsJsonAsync(url, payload, token);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await SafeReadBodyAsync(response);
                    Debug.WriteLine($"[ApiService] POST failed status={(int)response.StatusCode} url={url} body={body}");

                    // 4xx → không nên retry (lỗi logic, không phải transient)
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                        return null;

                    // 5xx → ném exception để Polly retry
                    response.EnsureSuccessStatusCode();
                }

                return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: token);
            }, ct);
        }
        catch (BrokenCircuitException)
        {
            Debug.WriteLine($"[ApiService] POST circuit open (fail-fast): {url}");
            return null;
        }
        catch (TimeoutRejectedException)
        {
            Debug.WriteLine($"[ApiService] POST total timeout: {url}");
            return null;
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

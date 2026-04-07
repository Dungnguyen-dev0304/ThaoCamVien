using System.Net.Http.Json;

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
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(12)
        };
    }

    public string BaseUrl { get; set; } = "http://10.0.2.2:5281";

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default) where T : class
    {
        try
        {
            var url = $"{BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
            return await _httpClient.GetFromJsonAsync<T>(url, ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload, CancellationToken ct = default)
        where TResponse : class
    {
        try
        {
            var url = $"{BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
            using var response = await _httpClient.PostAsJsonAsync(url, payload, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }
}

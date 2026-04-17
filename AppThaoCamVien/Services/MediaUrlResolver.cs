using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

/// <summary>
/// Helper dựng URL tới static files của WebThaoCamVien (wwwroot).
///
/// Ảnh POI được lưu ở:  WebThaoCamVien/wwwroot/images/pois/{fileName}
/// Audio POI được lưu ở: WebThaoCamVien/wwwroot/audio/pois/{fileName}
///
/// Web chạy ở port 5181 (xem launchSettings.json), trong khi Api chạy ở 5281.
/// Vì cả 2 service đều deploy trên cùng host, ta suy ra Web URL bằng cách
/// swap port từ ApiBaseUrl (đã được App.xaml.cs cấu hình cho emulator/device).
///
/// Nếu dev muốn override, có thể set preference "WebBaseUrl" riêng.
/// </summary>
public static class MediaUrlResolver
{
    private const string ApiPort = ":5281";
    private const string WebPort = ":5181";

    /// <summary>
    /// URL gốc của Web (wwwroot). Ưu tiên preference "WebBaseUrl";
    /// nếu không có, suy từ ApiService.BaseUrl bằng cách swap port 5281 → 5181.
    /// </summary>
    public static string WebBaseUrl
    {
        get
        {
            var pref = Preferences.Default.Get("WebBaseUrl", string.Empty);
            if (!string.IsNullOrWhiteSpace(pref))
                return pref.TrimEnd('/');

            var apiPref = Preferences.Default.Get("ApiBaseUrl", string.Empty);
            var apiUrl = string.IsNullOrWhiteSpace(apiPref)
                ? ApiService.ResolveDefaultApiUrl()
                : apiPref;

            // Nếu apiUrl chứa cổng 5281 → thay bằng 5181
            if (apiUrl.Contains(ApiPort))
                return apiUrl.Replace(ApiPort, WebPort).TrimEnd('/');

            // Fallback: giữ host nhưng ép port 5181
            return ResolveDefaultWebUrl().TrimEnd('/');
        }
    }

    /// <summary>
    /// URL mặc định cho Web theo loại thiết bị — song song với
    /// <see cref="ApiService.ResolveDefaultApiUrl"/>.
    /// </summary>
    internal static string ResolveDefaultWebUrl()
    {
#if ANDROID
        return DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://10.0.2.2:5181"
            : "http://192.168.0.101:5181";
#elif IOS
        return DeviceInfo.DeviceType == DeviceType.Virtual
            ? "http://localhost:5181"
            : "http://192.168.0.101:5181";
#else
        return "http://localhost:5181";
#endif
    }

    /// <summary>
    /// Trả về URL tuyệt đối đến ảnh POI trong wwwroot/images/pois.
    ///
    /// Chấp nhận:
    ///   - Chuỗi rỗng / null → trả chuỗi rỗng (binding sẽ không nạp ảnh).
    ///   - URL http(s) đầy đủ → giữ nguyên.
    ///   - Đường dẫn bắt đầu "/images/..." → prefix WebBaseUrl.
    ///   - Tên file thuần ("tiger.jpg") → {WebBaseUrl}/images/pois/{tên}.
    /// </summary>
    public static string ImageUrlFor(string? fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
            return string.Empty;

        var v = fileNameOrPath.Trim();

        if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return v;

        if (v.StartsWith("/"))
            return $"{WebBaseUrl}{v}";

        return $"{WebBaseUrl}/images/pois/{v}";
    }

    /// <summary>
    /// Trả về URL tuyệt đối đến file audio POI trong wwwroot/audio/pois.
    ///
    /// Chấp nhận:
    ///   - Chuỗi rỗng / null → trả chuỗi rỗng.
    ///   - URL http(s) đầy đủ → giữ nguyên.
    ///   - Đường dẫn bắt đầu "/audio/..." → prefix WebBaseUrl.
    ///   - Tên file thuần → {WebBaseUrl}/audio/pois/{tên}.
    /// </summary>
    public static string AudioUrlFor(string? fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
            return string.Empty;

        var v = fileNameOrPath.Trim();

        if (v.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            v.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return v;

        if (v.StartsWith("/"))
            return $"{WebBaseUrl}{v}";

        return $"{WebBaseUrl}/audio/pois/{v}";
    }
}

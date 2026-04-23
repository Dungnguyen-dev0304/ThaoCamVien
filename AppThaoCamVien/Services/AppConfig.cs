using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

/// <summary>
/// Nơi duy nhất cấu hình địa chỉ server cho app mobile.
///
/// Khi demo:
///   - Chạy emulator Android  → không cần đổi gì (tự dùng 10.0.2.2).
///   - Chạy máy Android thật  → đổi <see cref="LanIp"/> thành IP LAN của PC
///                              đang chạy Web + API (xem bằng `ipconfig`).
///                              PC và điện thoại phải cùng mạng WiFi.
///
/// Cổng:
///   - API  (ApiThaoCamVien)  = 5281
///   - Web  (WebThaoCamVien)  = 5181  — nơi chứa ảnh POI + audio trong wwwroot.
///
/// Cả Api và Web đều phải bind 0.0.0.0 (đã cấu hình trong launchSettings.json)
/// để máy khác trong LAN truy cập được.
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// IP LAN của PC khi test / demo trên máy Android thật.
    /// Đổi dòng này trước mỗi buổi demo nếu router cấp IP khác.
    /// </summary>
    //public const string LanIp = "192.168.1.10";

    // dịa chi ip trong lop
    //public const string LanIp = "192.168.31.126";

    // dia chi ip nhà dương
    public const string LanIp = "192.168.1.10";

    //ip cua dung
    // public const string LanIp = "192.168.0.100";


    public const string ApiPort = "5281";
    public const string WebPort = "5181";

    // ──────────────────────────────────────────────────────────────────
    // Các URL dẫn xuất — KHÔNG sửa trực tiếp, sửa LanIp phía trên.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>URL API mặc định cho máy Android thật.</summary>
    public static string RealDeviceApiUrl => $"http://{LanIp}:{ApiPort}";

    /// <summary>URL Web mặc định cho máy Android thật.</summary>
    public static string RealDeviceWebUrl => $"http://{LanIp}:{WebPort}";

    /// <summary>URL API mặc định cho Android emulator (alias loopback của host).</summary>
    public const string EmulatorApiUrl = "http://10.0.2.2:5281";

    /// <summary>URL Web mặc định cho Android emulator.</summary>
    public const string EmulatorWebUrl = "http://10.0.2.2:5181";

    // ──────────────────────────────────────────────────────────────────
    // Preferences hygiene — tránh IP cũ trong Preferences đè default mới.
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nếu Preferences["ApiBaseUrl"] hoặc ["WebBaseUrl"] trỏ về IP KHÔNG
    /// phải emulator (10.0.2.2) và KHÔNG phải <see cref="LanIp"/> hiện tại,
    /// thì đó là rác của lần test trước → xoá đi để default trong code
    /// (ResolveDefaultApiUrl) được dùng lại.
    /// Gọi 1 lần lúc app khởi động, TRƯỚC khi các service đọc Preferences.
    /// </summary>
    public static void EnsureFreshPreferences()
    {
        TryCleanup("ApiBaseUrl");
        TryCleanup("WebBaseUrl");

        System.Diagnostics.Debug.WriteLine(
            $"[AppConfig] LanIp={LanIp}  " +
            $"Api(real)={RealDeviceApiUrl}  Api(emu)={EmulatorApiUrl}");
    }

    private static void TryCleanup(string key)
    {
        var val = Preferences.Default.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(val)) return;

        bool isEmulator   = val.Contains("10.0.2.2");
        bool isCurrentLan = !string.IsNullOrWhiteSpace(LanIp) && val.Contains(LanIp);
        bool isLocalhost  = val.Contains("localhost") || val.Contains("127.0.0.1");

        if (isEmulator || isCurrentLan || isLocalhost) return;

        System.Diagnostics.Debug.WriteLine(
            $"[AppConfig] Stale pref {key}='{val}' (không match emulator/LanIp={LanIp}) → xoá");
        Preferences.Default.Remove(key);
    }
}

using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

/// <summary>
/// NÆ¡i duy nháº¥t cáº¥u hÃ¬nh Ä‘á»‹a chá»‰ server cho app mobile.
///
/// Khi demo:
///   - Cháº¡y emulator Android  â†’ khÃ´ng cáº§n Ä‘á»•i gÃ¬ (tá»± dÃ¹ng 10.0.2.2).
///   - Cháº¡y mÃ¡y Android tháº­t  â†’ Ä‘á»•i <see cref="LanIp"/> thÃ nh IP LAN cá»§a PC
///                              Ä‘ang cháº¡y Web + API (xem báº±ng `ipconfig`).
///                              PC vÃ  Ä‘iá»‡n thoáº¡i pháº£i cÃ¹ng máº¡ng WiFi.
///
/// Cá»•ng:
///   - API  (ApiThaoCamVien)  = 5281
///   - Web  (WebThaoCamVien)  = 5181  â€” nÆ¡i chá»©a áº£nh POI + audio trong wwwroot.
///
/// Cáº£ Api vÃ  Web Ä‘á»u pháº£i bind 0.0.0.0 (Ä‘Ã£ cáº¥u hÃ¬nh trong launchSettings.json)
/// Ä‘á»ƒ mÃ¡y khÃ¡c trong LAN truy cáº­p Ä‘Æ°á»£c.
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// IP LAN cá»§a PC khi test / demo trÃªn mÃ¡y Android tháº­t.
    /// Äá»•i dÃ²ng nÃ y trÆ°á»›c má»—i buá»•i demo náº¿u router cáº¥p IP khÃ¡c.
    /// </summary>
    //public const string LanIp = "192.168.1.10";

    // dá»‹a chi ip trong lop
    //public const string LanIp = "192.168.31.126";

    // dia chi ip nhÃ  dÆ°Æ¡ng
    public const string LanIp = "172.20.10.6";
    //public const string LanIp = "192.168.1.10";

    // public const string LanIp = "172.20.10.2"; // id của 5g điện thoại 
    //public const string LanIp = "192.168.0.102"; // id của wifi nha TD2
    // 90218fe308e7fdaf8396a95d087177124aec00c6

    //ip cua dung
    // public const string LanIp = "192.168.0.100";



    public const string ApiPort = "5281";
    public const string WebPort = "5181";

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // CÃ¡c URL dáº«n xuáº¥t â€” KHÃ”NG sá»­a trá»±c tiáº¿p, sá»­a LanIp phÃ­a trÃªn.
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>URL API máº·c Ä‘á»‹nh cho mÃ¡y Android tháº­t.</summary>
    public static string RealDeviceApiUrl => $"http://{LanIp}:{ApiPort}";

    /// <summary>URL Web máº·c Ä‘á»‹nh cho mÃ¡y Android tháº­t.</summary>
    public static string RealDeviceWebUrl => $"http://{LanIp}:{WebPort}";

    /// <summary>URL API máº·c Ä‘á»‹nh cho Android emulator (alias loopback cá»§a host).</summary>
    public const string EmulatorApiUrl = "http://10.0.2.2:5281";

    /// <summary>URL Web máº·c Ä‘á»‹nh cho Android emulator.</summary>
    public const string EmulatorWebUrl = "http://10.0.2.2:5181";

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Preferences hygiene â€” trÃ¡nh IP cÅ© trong Preferences Ä‘Ã¨ default má»›i.
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Náº¿u Preferences["ApiBaseUrl"] hoáº·c ["WebBaseUrl"] trá» vá» IP KHÃ”NG
    /// pháº£i emulator (10.0.2.2) vÃ  KHÃ”NG pháº£i <see cref="LanIp"/> hiá»‡n táº¡i,
    /// thÃ¬ Ä‘Ã³ lÃ  rÃ¡c cá»§a láº§n test trÆ°á»›c â†’ xoÃ¡ Ä‘i Ä‘á»ƒ default trong code
    /// (ResolveDefaultApiUrl) Ä‘Æ°á»£c dÃ¹ng láº¡i.
    /// Gá»i 1 láº§n lÃºc app khá»Ÿi Ä‘á»™ng, TRÆ¯á»šC khi cÃ¡c service Ä‘á»c Preferences.
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
            $"[AppConfig] Stale pref {key}='{val}' (khÃ´ng match emulator/LanIp={LanIp}) â†’ xoÃ¡");
        Preferences.Default.Remove(key);
    }
}

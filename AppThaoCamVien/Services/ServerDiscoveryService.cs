using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

/// <summary>
/// Tự động tìm địa chỉ server API trong mạng LAN bằng UDP broadcast.
/// Không cần người dùng nhập IP thủ công.
///
/// Giao thức:
///   App phát UDP broadcast tới port 5282: "THAOCAMP_DISCOVER"
///   Server (UdpDiscoveryService) phản hồi:  "http://192.168.x.y:5281"
///
/// Cache 5 phút — không broadcast lại nếu đã tìm thấy gần đây.
/// </summary>
public class ServerDiscoveryService
{
    private const int    DiscoveryPort  = 5282;
    private const string DiscoverMsg    = "THAOCAMP_DISCOVER";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim _sem = new(1, 1);

    private string?  _cached;
    private DateTime _cachedAt = DateTime.MinValue;

    /// <summary>
    /// Phát UDP broadcast và chờ server phản hồi.
    /// Trả về URL server (vd "http://192.168.1.5:5281") hoặc null nếu không tìm thấy.
    /// Thread-safe — nhiều caller cùng lúc chỉ tạo 1 broadcast duy nhất.
    /// </summary>
    public async Task<string?> DiscoverAsync(int timeoutMs = 3000)
    {
        // Trả cache còn hợp lệ
        if (_cached != null && DateTime.Now - _cachedAt < CacheDuration)
        {
            Debug.WriteLine($"[Discovery] Using cached URL: {_cached}");
            return _cached;
        }

        await _sem.WaitAsync().ConfigureAwait(false);
        try
        {
            // Re-check sau khi lấy lock (double-check pattern)
            if (_cached != null && DateTime.Now - _cachedAt < CacheDuration)
                return _cached;

            return await BroadcastAndReceiveAsync(timeoutMs);
        }
        finally
        {
            _sem.Release();
        }
    }

    private async Task<string?> BroadcastAndReceiveAsync(int timeoutMs)
    {
        Debug.WriteLine("[Discovery] Broadcasting...");
        try
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;

            var payload = Encoding.UTF8.GetBytes(DiscoverMsg);
            await udp.SendAsync(payload, payload.Length,
                new IPEndPoint(IPAddress.Broadcast, DiscoveryPort)).ConfigureAwait(false);

            using var cts = new CancellationTokenSource(timeoutMs);
            var result = await udp.ReceiveAsync(cts.Token).ConfigureAwait(false);
            var url    = Encoding.UTF8.GetString(result.Buffer).Trim();

            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                _cached   = url;
                _cachedAt = DateTime.Now;
                Debug.WriteLine($"[Discovery] Found server: {url}");
                return url;
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[Discovery] Timeout — no server responded.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Discovery] Error: {ex.Message}");
        }

        return null;
    }

    /// <summary>Xoá cache để lần sau broadcast lại (dùng khi đổi mạng WiFi).</summary>
    public void InvalidateCache()
    {
        _cached   = null;
        _cachedAt = DateTime.MinValue;
    }

    /// <summary>
    /// Thử discover và nếu thành công thì lưu vào Preferences ngay.
    /// Tiện dùng ở các điểm không có await context đầy đủ.
    /// Trả về URL tìm được hoặc null.
    /// </summary>
    public async Task<string?> DiscoverAndSaveAsync(int timeoutMs = 3000)
    {
        var url = await DiscoverAsync(timeoutMs).ConfigureAwait(false);
        if (url != null)
        {
            Preferences.Default.Set("ApiBaseUrl", url);
            Debug.WriteLine($"[Discovery] Saved ApiBaseUrl = {url}");
        }
        return url;
    }
}

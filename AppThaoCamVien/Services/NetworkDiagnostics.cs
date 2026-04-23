using System.Net;
using System.Net.Sockets;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace AppThaoCamVien.Services;

/// <summary>
/// Phân loại lỗi mạng thành thông báo tiếng Việt rõ ràng.
///
/// Vấn đề gốc: HttpRequestException wraps lỗi platform-specific:
///   - Android: Java.Net.ConnectException, Java.Net.UnknownHostException...
///   - iOS:     NSURLError (-1004, -1009, -1001...)
///   - .NET:    SocketException, SocketError enum
///
/// Class này chuẩn hoá TẤT CẢ về một thông điệp người dùng hiểu được,
/// đồng thời giữ nguyên chi tiết kỹ thuật trong debug log.
/// </summary>
public static class NetworkDiagnostics
{
    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>Phân loại exception → thông báo lỗi tiếng Việt ngắn gọn.</summary>
    public static string Classify(Exception ex) => ex switch
    {
        // ── Polly policy exceptions ─────────────────────────────────────────
        BrokenCircuitException =>
            "Server không khả dụng — mạch ngắt sau nhiều lỗi liên tiếp. Thử lại sau 30 giây.",

        TimeoutRejectedException =>
            "Timeout: server không phản hồi trong 15 giây (Polly total timeout).",

        // ── HTTP request exceptions ─────────────────────────────────────────
        HttpRequestException hre when hre.StatusCode == HttpStatusCode.Unauthorized =>
            "Lỗi 401 Unauthorized: sai thông tin xác thực.",

        HttpRequestException hre when hre.StatusCode == HttpStatusCode.Forbidden =>
            "Lỗi 403 Forbidden: không có quyền truy cập endpoint này.",

        HttpRequestException hre when hre.StatusCode == HttpStatusCode.NotFound =>
            "Lỗi 404 Not Found: endpoint không tồn tại — kiểm tra lại URL API.",

        HttpRequestException hre when hre.StatusCode == HttpStatusCode.ServiceUnavailable =>
            "Lỗi 503 Service Unavailable: server đang quá tải hoặc đang khởi động.",

        HttpRequestException hre when (int?)hre.StatusCode >= 500 =>
            $"Lỗi server {(int)hre.StatusCode!.Value}: {hre.Message}",

        HttpRequestException hre when (int?)hre.StatusCode is >= 400 and < 500 =>
            $"Lỗi client {(int)hre.StatusCode!.Value}: {hre.Message}",

        // ── No status code → network-level failure ──────────────────────────
        HttpRequestException hre when hre.StatusCode == null =>
            ClassifyNoStatusCode(hre),

        // ── Task / cancellation ─────────────────────────────────────────────
        TaskCanceledException tce when !tce.CancellationToken.IsCancellationRequested =>
            "Timeout: yêu cầu mất quá nhiều thời gian, kết nối bị huỷ tự động.",

        TaskCanceledException =>
            "Yêu cầu bị huỷ bởi người dùng hoặc app.",

        // ── Socket exceptions (đôi khi surface trực tiếp) ───────────────────
        SocketException se => ClassifySocketError(se.SocketErrorCode),

        // ── Fallback ────────────────────────────────────────────────────────
        _ => $"Lỗi không xác định ({ex.GetType().Name}): {ex.Message}"
    };

    /// <summary>
    /// Kiểm tra có phải lỗi "server chưa chạy / connection refused" không.
    /// Dùng để quyết định có nên hiển thị gợi ý khởi động server.
    /// </summary>
    public static bool IsConnectionRefused(Exception ex)
    {
        if (ex is HttpRequestException { StatusCode: null } hre)
        {
            var msg = GetFullMessage(hre);
            return msg.Contains("refused", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("ECONNREFUSED", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("actively refused", StringComparison.OrdinalIgnoreCase);
        }
        if (ex is SocketException se)
            return se.SocketErrorCode == SocketError.ConnectionRefused;
        return false;
    }

    /// <summary>
    /// Kiểm tra có phải lỗi DNS không (hostname không phân giải được).
    /// Thiết bị thật thường gặp khi URL vẫn trỏ về "localhost" hoặc hostname lạ.
    /// </summary>
    public static bool IsDnsFailure(Exception ex)
    {
        if (ex is HttpRequestException { StatusCode: null } hre)
        {
            var msg = GetFullMessage(hre);
            return msg.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("No such host", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Name does not resolve", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("nodename nor servname provided", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("getaddrinfo failed", StringComparison.OrdinalIgnoreCase);
        }
        if (ex is SocketException se)
            return se.SocketErrorCode == SocketError.HostNotFound
                || se.SocketErrorCode == SocketError.NoData;
        return false;
    }

    /// <summary>
    /// Kiểm tra có phải lỗi "không có WiFi / mạng không khả dụng".
    /// </summary>
    public static bool IsNetworkUnreachable(Exception ex)
    {
        if (ex is HttpRequestException { StatusCode: null } hre)
        {
            var msg = GetFullMessage(hre);
            return msg.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("ENETUNREACH", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("No network", StringComparison.OrdinalIgnoreCase);
        }
        if (ex is SocketException se)
            return se.SocketErrorCode == SocketError.NetworkUnreachable
                || se.SocketErrorCode == SocketError.NetworkDown;
        return false;
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Phân loại HttpRequestException không có status code.
    /// Đây là nơi lỗi network thật ẩn bên trong InnerException.
    ///
    /// Platform messages:
    ///   Android: "Connection refused (ip:port)", "Name or service not known"
    ///            (wrap Java.Net.ConnectException / Java.Net.UnknownHostException)
    ///   iOS:     "The request timed out." (-1001), "Could not connect to the server." (-1004)
    ///            "The Internet connection appears to be offline." (-1009)
    ///   Windows: "No connection could be made because the target machine actively refused it."
    /// </summary>
    private static string ClassifyNoStatusCode(HttpRequestException hre)
    {
        var msg = GetFullMessage(hre);

        // Connection refused — server chưa chạy hoặc sai port
        if (msg.Contains("refused", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("ECONNREFUSED", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("Could not connect to the server", StringComparison.OrdinalIgnoreCase))
            return "Connection refused: server chưa chạy, sai IP hoặc sai port. " +
                   "Kiểm tra API đang chạy và máy tính cùng WiFi với điện thoại.";

        // DNS failure — hostname không phân giải được
        if (msg.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("No such host", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("getaddrinfo failed", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("nodename nor servname", StringComparison.OrdinalIgnoreCase))
            return "DNS error: không phân giải được hostname. " +
                   "Thiết bị thật không dùng 'localhost' — cần IP LAN (vd: 192.168.1.x).";

        // Network unreachable — không có WiFi hoặc thiết bị offline
        if (msg.Contains("Network is unreachable", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("ENETUNREACH", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("Internet connection appears to be offline", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("No network", StringComparison.OrdinalIgnoreCase))
            return "Không có mạng: thiết bị không kết nối WiFi hoặc đang offline.";

        // iOS timeout message
        if (msg.Contains("timed out", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("request timed out", StringComparison.OrdinalIgnoreCase))
            return "Timeout: server không phản hồi. " +
                   "Kiểm tra IP đúng và server đang lắng nghe trên cổng 5281.";

        // Android SSL / cert issue (debug bypass chưa active)
        if (msg.Contains("SSL", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("certificate", StringComparison.OrdinalIgnoreCase)
         || msg.Contains("handshake", StringComparison.OrdinalIgnoreCase))
            return "SSL/TLS error: chứng chỉ không hợp lệ. " +
                   "Dùng HTTP (không phải HTTPS) cho server LAN nội bộ.";

        // SocketException wrapped bên trong
        if (hre.InnerException is SocketException inner)
            return ClassifySocketError(inner.SocketErrorCode);

        // Fallback với raw message
        return $"Network error: {msg}";
    }

    private static string ClassifySocketError(SocketError code) => code switch
    {
        SocketError.ConnectionRefused  => "Connection refused: server chưa chạy hoặc sai port.",
        SocketError.TimedOut           => "Socket timeout: server không phản hồi.",
        SocketError.HostNotFound
        or SocketError.NoData          => "DNS: không tìm thấy hostname.",
        SocketError.NetworkUnreachable
        or SocketError.NetworkDown     => "Không có mạng / WiFi.",
        SocketError.ConnectionReset    => "Kết nối bị reset bởi server.",
        _                              => $"Socket error ({code})"
    };

    /// <summary>Lấy toàn bộ message chain (Exception + InnerException) để tìm kiếm.</summary>
    private static string GetFullMessage(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        var current = ex;
        while (current != null)
        {
            sb.Append(current.Message).Append(' ');
            current = current.InnerException;
        }
        return sb.ToString();
    }
}

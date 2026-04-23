using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ApiThaoCamVien.Services;

/// <summary>
/// UDP Discovery Responder — giúp app mobile tự tìm địa chỉ server mà không cần
/// người dùng nhập IP thủ công.
///
/// Giao thức:
///   Mobile phát broadcast UDP tới port 5282: "THAOCAMP_DISCOVER"
///   Server phản hồi unicast cùng port:       "http://192.168.x.y:5281"
///
/// Port 5282 không xung đột với API (5281) hay Web (5181).
/// </summary>
public class UdpDiscoveryService : BackgroundService
{
    private const int    DiscoveryPort = 5282;
    private const string DiscoverMsg   = "THAOCAMP_DISCOVER";

    private readonly ILogger<UdpDiscoveryService> _logger;

    public UdpDiscoveryService(ILogger<UdpDiscoveryService> logger)
        => _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient();
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            udp.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[Discovery] Cannot bind UDP port {Port}: {Msg}. Auto-discovery disabled.", DiscoveryPort, ex.Message);
            return;
        }

        _logger.LogInformation("[Discovery] UDP responder listening on port {Port}", DiscoveryPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(stoppingToken);
                var incoming = Encoding.UTF8.GetString(result.Buffer).Trim();

                if (!string.Equals(incoming, DiscoverMsg, StringComparison.Ordinal))
                    continue;

                var myIp    = GetLanIp();
                var payload = Encoding.UTF8.GetBytes($"http://{myIp}:5281");
                await udp.SendAsync(payload, payload.Length, result.RemoteEndPoint);

                _logger.LogInformation("[Discovery] Replied to {Remote} → http://{Ip}:5281",
                    result.RemoteEndPoint, myIp);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[Discovery] UDP receive error");
                await Task.Delay(500, stoppingToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("[Discovery] UDP responder stopped.");
    }

    /// <summary>
    /// Lấy IP LAN của máy PC (giao diện mạng chính, không phải loopback).
    /// Dùng trick "UDP connect": OS chọn interface phù hợp mà không gửi packet nào.
    /// </summary>
    private static string GetLanIp()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Connect("8.8.8.8", 80);
            return ((IPEndPoint)s.LocalEndPoint!).Address.ToString();
        }
        catch
        {
            // Fallback: quét NetworkInterface, lấy IPv4 đầu tiên không phải loopback
            return System.Net.NetworkInformation.NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(i => i.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                .SelectMany(i => i.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                         && !IPAddress.IsLoopback(a.Address))
                .Select(a => a.Address.ToString())
                .FirstOrDefault() ?? "127.0.0.1";
        }
    }
}

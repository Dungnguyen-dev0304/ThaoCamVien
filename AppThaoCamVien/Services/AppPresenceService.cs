using System.Threading;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;

namespace AppThaoCamVien.Services;

/// <summary>
/// Sends periodic heartbeat to API when online so admin can show live app session count.
/// </summary>
public sealed class AppPresenceService
{
    public const string PrefSessionId = "AppPresenceSessionId";

    private readonly ApiService _api;
    private int _started;

    public AppPresenceService(ApiService api) => _api = api;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;
        _ = LoopAsync();
    }

    private static string EnsureSessionId()
    {
        var id = Preferences.Default.Get(PrefSessionId, string.Empty);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(PrefSessionId, id);
        }
        return id;
    }

    private async Task LoopAsync()
    {
        await Task.Delay(2000).ConfigureAwait(false);
        while (true)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                {
                    var sid = EnsureSessionId();
                    await _api.PostPresencePingAsync(sid, null, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                // offline / transient
            }

            try
            {
                // Ping 2s/lần để admin phát hiện offline trong ~5s sau khi thoát app.
                // Đánh đổi: tốn pin + traffic. Chỉ hợp với demo, KHÔNG phù hợp deploy thật.
                await Task.Delay(2_000).ConfigureAwait(false);
            }
            catch
            {
                break;
            }
        }
    }
}

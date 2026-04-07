using Microsoft.Maui.Networking;

namespace AppThaoCamVien.Services;

/// <summary>
/// Mô phỏng tải gói offline có trạng thái lưu, có thể tiếp tục sau khi mất mạng.
/// Tiến độ lưu trong Preferences; kiểm tra <see cref="Connectivity"/> mỗi bước.
/// </summary>
public sealed class OfflineBundleDownloadService
{
    public const string PrefProgress = "OfflineBundleProgress01";
    public const string PrefCompleted = "OfflineBundleReady";

    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private bool _running;

    public event EventHandler<double>? ProgressChanged;
    public event EventHandler<string>? StatusChanged;

    public double Progress => Preferences.Get(PrefProgress, 0d);

    public bool IsCompleted => Preferences.Get(PrefCompleted, false);

    public bool IsRunning
    {
        get { lock (_gate) return _running; }
    }

    /// <summary>Đặt lại tiến độ (ví dụ sau khi xóa cache trong Cài đặt).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _running = false;
        }

        Preferences.Remove(PrefProgress);
        Preferences.Set(PrefCompleted, false);
        ProgressChanged?.Invoke(this, 0);
    }

    /// <summary>Bắt đầu hoặc tiếp tục tải từ tiến độ đã lưu.</summary>
    public Task DownloadAsync(CancellationToken externalCt = default)
    {
        lock (_gate)
        {
            if (_running)
                return Task.CompletedTask;
            _running = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        }

        return Task.Run(async () =>
        {
            try
            {
                await RunDownloadLoopAsync(_cts!.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    _running = false;
                    _cts?.Dispose();
                    _cts = null;
                }
            }
        });
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _cts?.Cancel();
        }
    }

    private async Task RunDownloadLoopAsync(CancellationToken ct)
    {
        var p = Preferences.Get(PrefProgress, 0d);
        if (p >= 1d)
        {
            Preferences.Set(PrefCompleted, true);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ProgressChanged?.Invoke(this, 1d);
                StatusChanged?.Invoke(this, "completed");
            });
            return;
        }

        const double step = 0.008;
        const int delayMs = 45;

        while (p < 1d)
        {
            ct.ThrowIfCancellationRequested();

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusChanged?.Invoke(this, "network_lost");
                    ProgressChanged?.Invoke(this, p);
                });
                return;
            }

            await Task.Delay(delayMs, ct).ConfigureAwait(false);
            p = Math.Min(1d, p + step);
            Preferences.Set(PrefProgress, p);

            var copy = p;
            MainThread.BeginInvokeOnMainThread(() => ProgressChanged?.Invoke(this, copy));
        }

        Preferences.Set(PrefCompleted, true);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ProgressChanged?.Invoke(this, 1d);
            StatusChanged?.Invoke(this, "completed");
        });
    }
}

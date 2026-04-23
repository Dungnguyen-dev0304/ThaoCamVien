using Microsoft.Maui.Networking;

namespace AppThaoCamVien.Services;

/// <summary>
/// Keeps local SQLite cache in sync with API when online.
/// - Triggers a sync when internet becomes available.
/// - Periodically syncs in background (best-effort).
/// </summary>
public sealed class BackgroundDataSyncService : IDisposable
{
    private readonly DatabaseService _db;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _started;
    private CancellationTokenSource? _cts;

    /// <summary>Minimum interval between background syncs.</summary>
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(2);

    public BackgroundDataSyncService(DatabaseService db) => _db = db;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;

        _cts = new CancellationTokenSource();
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        _ = LoopAsync(_cts.Token);
    }

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        try
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
                await TrySyncOnceAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // best-effort
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        // Small delay so app startup isn't blocked by network/DNS.
        try { await Task.Delay(2500, ct).ConfigureAwait(false); } catch { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
                    await TrySyncOnceAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                // best-effort
            }

            try { await Task.Delay(SyncInterval, ct).ConfigureAwait(false); }
            catch { break; }
        }
    }

    private async Task TrySyncOnceAsync(CancellationToken ct)
    {
        // Avoid overlapping syncs (e.g., a page LoadAsync calls SyncDataFromApiAsync at same time).
        if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
            return;

        try
        {
            await _db.SyncDataFromApiAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        try
        {
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
        }
        catch { }

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
        catch { }
    }
}


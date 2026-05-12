using ApiThaoCamVien.Models;
using ApiThaoCamVien.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;

namespace ThaoCamVien.Tests.Queue;

/// <summary>
/// Automation test cho hàng đợi FIFO (kịch bản "50 học sinh cùng quét 1 QR").
///
/// Dùng SQLite in-memory thay SQL Server thật:
///   - Chạy nhanh, không cần SQL Server cài đặt
///   - IDENTITY auto-increment hành xử giống SQL Server (tuần tự tăng)
///   - Mỗi test isolated: tạo DB riêng, dispose sau test
///
/// Chạy: dotnet test --filter Queue
/// </summary>
public class QrQueueServiceTests : IDisposable
{
    private readonly string _dbName;
    private readonly string _connStr;
    private readonly SqliteConnection _keepAlive;

    public QrQueueServiceTests()
    {
        // Tên DB unique cho mỗi instance test → isolated.
        // Shared cache mode cho phép nhiều connection cùng thấy DB này
        // (cần thiết cho test concurrent 50 ticket).
        _dbName = $"qrqueue-test-{Guid.NewGuid():N}";
        _connStr = $"DataSource=file:{_dbName}?mode=memory&cache=shared";

        // Connection "keepalive" mở suốt vòng đời test để DB không bị
        // SQLite giải phóng khi không có connection nào active.
        _keepAlive = new SqliteConnection(_connStr);
        _keepAlive.Open();

        // Tạo schema 1 lần
        using var cmd = _keepAlive.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE queue_tickets (
                ticket_id INTEGER PRIMARY KEY AUTOINCREMENT,
                poi_id INTEGER NOT NULL,
                session_id TEXT NOT NULL,
                joined_utc TEXT NOT NULL,
                started_playing_utc TEXT NULL,
                finished_utc TEXT NULL
            );
            CREATE INDEX IX_queue_tickets_poi_active ON queue_tickets(poi_id, finished_utc);
            CREATE INDEX IX_queue_tickets_joined ON queue_tickets(joined_utc);
        ";
        cmd.ExecuteNonQuery();
    }

    private WebContext NewContext()
    {
        // Mỗi context có connection riêng — chia sẻ DB qua shared cache
        var options = new DbContextOptionsBuilder<WebContext>()
            .UseSqlite(_connStr)
            .Options;
        return new WebContext(options);
    }

    private QrQueueService NewService(WebContext ctx) => new(ctx);

    public void Dispose() => _keepAlive.Dispose();

    // ─── TEST 1 ────────────────────────────────────────────────────────────
    [Fact]
    public async Task SingleJoin_returns_position_1_total_1()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var r = await svc.JoinAsync(poiId: 1, sessionId: "session-001", CancellationToken.None);

        Assert.True(r.TicketId > 0);
        Assert.Equal(1, r.Position);
        Assert.Equal(1, r.Total);
    }

    // ─── TEST 2 ────────────────────────────────────────────────────────────
    [Fact]
    public async Task SequentialJoins_get_increasing_positions()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var r1 = await svc.JoinAsync(1, "s-A", CancellationToken.None);
        var r2 = await svc.JoinAsync(1, "s-B", CancellationToken.None);
        var r3 = await svc.JoinAsync(1, "s-C", CancellationToken.None);

        Assert.Equal(1, r1.Position);
        Assert.Equal(2, r2.Position);
        Assert.Equal(3, r3.Position);
        Assert.Equal(3, r3.Total);
    }

    // ─── TEST 3 ────────────────────────────────────────────────────────────
    [Fact]
    public async Task SameSession_rejoining_same_POI_returns_same_ticket()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var r1 = await svc.JoinAsync(1, "same-session-xyz", CancellationToken.None);
        var r2 = await svc.JoinAsync(1, "same-session-xyz", CancellationToken.None);

        Assert.Equal(r1.TicketId, r2.TicketId);
        Assert.Equal(1, r2.Position);
        Assert.Equal(1, r2.Total);   // Không tạo ticket mới
    }

    // ─── TEST 4 ────────────────────────────────────────────────────────────
    [Fact]
    public async Task Leave_makes_next_become_position_1()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var r1 = await svc.JoinAsync(1, "s-A", CancellationToken.None);
        var r2 = await svc.JoinAsync(1, "s-B", CancellationToken.None);
        var r3 = await svc.JoinAsync(1, "s-C", CancellationToken.None);

        // Người 1 rời hàng
        var left = await svc.LeaveAsync(r1.TicketId, CancellationToken.None);
        Assert.True(left);

        // Người 2 bây giờ phải ở position 1
        var status2 = await svc.GetStatusAsync(r2.TicketId, CancellationToken.None);
        Assert.NotNull(status2);
        Assert.Equal(1, status2!.Position);
        Assert.Equal(2, status2.Total);   // Tổng còn 2

        // Người 3 ở position 2
        var status3 = await svc.GetStatusAsync(r3.TicketId, CancellationToken.None);
        Assert.NotNull(status3);
        Assert.Equal(2, status3!.Position);
    }

    // ─── TEST 5 ────────────────────────────────────────────────────────────
    [Fact]
    public async Task Different_POIs_have_independent_queues()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        // 3 người vào POI 1
        await svc.JoinAsync(1, "p1-a", CancellationToken.None);
        await svc.JoinAsync(1, "p1-b", CancellationToken.None);
        await svc.JoinAsync(1, "p1-c", CancellationToken.None);

        // 2 người vào POI 2 — phải có position 1 và 2 (không phải 4 và 5)
        var r2a = await svc.JoinAsync(2, "p2-a", CancellationToken.None);
        var r2b = await svc.JoinAsync(2, "p2-b", CancellationToken.None);

        Assert.Equal(1, r2a.Position);
        Assert.Equal(2, r2b.Position);
        Assert.Equal(2, r2b.Total);
    }

    // ─── TEST 6 — KỊCH BẢN CHÍNH: 50 NGƯỜI CÙNG QUÉT ───────────────────────
    [Fact]
    public async Task FIFO_50_concurrent_joins_get_50_unique_sequential_positions()
    {
        const int N = 50;

        // Mỗi join dùng DbContext riêng (giả lập 50 HTTP request độc lập)
        var tasks = Enumerable.Range(0, N).Select(async i =>
        {
            // Stagger nhỏ để tạo race condition realistic
            await Task.Delay(Random.Shared.Next(0, 30));

            using var ctx = NewContext();
            var svc = NewService(ctx);
            return await svc.JoinAsync(poiId: 1, sessionId: $"concurrent-{i:D3}", CancellationToken.None);
        }).ToList();

        var results = await Task.WhenAll(tasks);

        // 50 ticket UNIQUE
        var uniqueTickets = results.Select(r => r.TicketId).Distinct().Count();
        Assert.Equal(N, uniqueTickets);

        // Sau khi sort theo TicketId, position phải là 1,2,3,...,50
        var positions = results.OrderBy(r => r.TicketId).Select(r => r.Position).ToList();
        Assert.Equal(Enumerable.Range(1, N), positions);

        // Tổng ai cũng thấy = 50 (tại thời điểm cuối)
        // Note: total mỗi join thấy lúc đó có thể nhỏ hơn nếu join chưa xong
        // → check ở status sau cùng:
        using var ctx2 = NewContext();
        var svc2 = NewService(ctx2);
        var lastStatus = await svc2.GetStatusAsync(results[0].TicketId, CancellationToken.None);
        Assert.NotNull(lastStatus);
        Assert.Equal(N, lastStatus!.Total);
    }

    // ─── TEST 7 ────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetStatus_position1_sets_StartedPlayingUtc()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var r = await svc.JoinAsync(1, "auto-play-test", CancellationToken.None);
        Assert.Equal(1, r.Position);

        // Trước khi gọi GetStatus, StartedPlayingUtc phải NULL
        using (var verify = NewContext())
        {
            var t = await verify.QueueTickets.FindAsync(r.TicketId);
            Assert.NotNull(t);
            Assert.Null(t!.StartedPlayingUtc);
        }

        var status = await svc.GetStatusAsync(r.TicketId, CancellationToken.None);
        Assert.True(status!.IsPlaying);

        // Sau khi GetStatus chạm position=1, StartedPlayingUtc phải được set
        using (var verify = NewContext())
        {
            var t = await verify.QueueTickets.FindAsync(r.TicketId);
            Assert.NotNull(t!.StartedPlayingUtc);
        }
    }

    // ─── TEST 8 ────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetStatus_for_finished_ticket_returns_null()
    {
        using var ctx = NewContext();
        var svc = NewService(ctx);

        var r = await svc.JoinAsync(1, "finished-test", CancellationToken.None);
        await svc.LeaveAsync(r.TicketId, CancellationToken.None);

        var status = await svc.GetStatusAsync(r.TicketId, CancellationToken.None);
        Assert.Null(status);
    }
}

using ApiThaoCamVien.Models;
using Microsoft.EntityFrameworkCore;
using SharedThaoCamVien.Models;

namespace ApiThaoCamVien.Services;

/// <summary>
/// FIFO atomic queue cho kịch bản "50 học sinh cùng quét 1 QR":
/// SQL Server IDENTITY tự tuần tự hoá → ai INSERT trước → TicketId nhỏ hơn
/// → position 1 → được phát audio trước. Không cần lock thủ công.
/// </summary>
public sealed class QrQueueService
{
    private readonly WebContext _ctx;

    public QrQueueService(WebContext ctx) => _ctx = ctx;

    // CANCELLED sentinel — khi client cancel (Ctrl+C python), service trả về
    // record này thay vì throw OperationCanceledException. Controller check
    // r.Position == -2 thì biết là cancel và trả 499. Không bao giờ exception
    // bubble ra ngoài service → Visual Studio không hiện popup user-unhandled.
    private static readonly JoinResult Cancelled = new(0, -2, 0);

    public async Task<JoinResult> JoinAsync(int poiId, string sessionId, CancellationToken ct)
    {
        try
        {
            var existing = await _ctx.QueueTickets
                .Where(t => t.PoiId == poiId
                         && t.SessionId == sessionId
                         && t.FinishedUtc == null)
                .OrderByDescending(t => t.TicketId)
                .FirstOrDefaultAsync(ct);

            QueueTicket ticket;
            if (existing != null)
            {
                ticket = existing;
            }
            else
            {
                ticket = new QueueTicket
                {
                    PoiId = poiId,
                    SessionId = sessionId,
                    JoinedUtc = DateTime.UtcNow,
                };
                _ctx.QueueTickets.Add(ticket);
                await _ctx.SaveChangesAsync(ct);
            }

            var (position, total) = await ComputePositionAsync(poiId, ticket.TicketId, ct);
            return new JoinResult(ticket.TicketId, position, total);
        }
        catch (OperationCanceledException)
        {
            // Client cancel — trả sentinel, không re-throw.
            return Cancelled;
        }
    }

    public async Task<StatusResult?> GetStatusAsync(long ticketId, CancellationToken ct)
    {
        try
        {
            var ticket = await _ctx.QueueTickets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);
            if (ticket == null || ticket.FinishedUtc != null) return null;

            var (position, total) = await ComputePositionAsync(ticket.PoiId, ticketId, ct);
            if (position == -2) return null;   // cancelled inside ComputePositionAsync

            if (position == 1 && ticket.StartedPlayingUtc == null)
            {
                var live = await _ctx.QueueTickets.FindAsync(new object[] { ticketId }, ct);
                if (live != null && live.StartedPlayingUtc == null)
                {
                    live.StartedPlayingUtc = DateTime.UtcNow;
                    await _ctx.SaveChangesAsync(ct);
                }
            }

            return new StatusResult(ticketId, position, total,
                IsPlaying: ticket.StartedPlayingUtc != null || position == 1);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public async Task<bool> LeaveAsync(long ticketId, CancellationToken ct)
    {
        try
        {
            var ticket = await _ctx.QueueTickets.FindAsync(new object[] { ticketId }, ct);
            if (ticket == null) return false;
            if (ticket.FinishedUtc == null)
            {
                ticket.FinishedUtc = DateTime.UtcNow;
                await _ctx.SaveChangesAsync(ct);
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Position = số ticket có TicketId &lt;= ticketId trong cùng POI và chưa Finished.
    /// Total = tổng ticket active của POI.
    /// Trả (-2, 0) nếu cancel. Trả (-1, total) nếu ticket không còn active.
    ///
    /// 2 COUNT query nhẹ thay vì load toàn list — chịu được 50+ device đồng thời.
    /// </summary>
    private async Task<(int position, int total)> ComputePositionAsync(
        int poiId, long ticketId, CancellationToken ct)
    {
        try
        {
            var total = await _ctx.QueueTickets.AsNoTracking()
                .CountAsync(t => t.PoiId == poiId && t.FinishedUtc == null, ct);

            if (total == 0) return (-1, 0);

            // Position = số ticket cùng POI, chưa Finished, có TicketId <= mình.
            // Vì TicketId là IDENTITY tăng dần → cách này tương đương ROW_NUMBER.
            var position = await _ctx.QueueTickets.AsNoTracking()
                .CountAsync(t => t.PoiId == poiId
                              && t.FinishedUtc == null
                              && t.TicketId <= ticketId, ct);

            return position == 0 ? (-1, total) : (position, total);
        }
        catch (OperationCanceledException)
        {
            return (-2, 0);
        }
    }
}

public sealed record JoinResult(long TicketId, int Position, int Total);
public sealed record StatusResult(long TicketId, int Position, int Total, bool IsPlaying);

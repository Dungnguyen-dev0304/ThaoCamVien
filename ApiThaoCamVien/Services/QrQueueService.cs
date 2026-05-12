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

    public async Task<JoinResult> JoinAsync(int poiId, string sessionId, CancellationToken ct)
    {
        // Nếu session đã đang trong hàng cùng POI (chưa Finished) → tái sử dụng,
        // tránh user spam click tạo nhiều ticket.
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
            await _ctx.SaveChangesAsync(ct);   // IDENTITY tự tăng — atomic
        }

        var (position, total) = await ComputePositionAsync(poiId, ticket.TicketId, ct);
        return new JoinResult(ticket.TicketId, position, total);
    }

    public async Task<StatusResult?> GetStatusAsync(long ticketId, CancellationToken ct)
    {
        var ticket = await _ctx.QueueTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);
        if (ticket == null || ticket.FinishedUtc != null) return null;

        var (position, total) = await ComputePositionAsync(ticket.PoiId, ticketId, ct);

        // Đến lượt: đánh dấu StartedPlayingUtc lần đầu tiên hit position 1.
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

    public async Task<bool> LeaveAsync(long ticketId, CancellationToken ct)
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

    /// <summary>
    /// Position = ROW_NUMBER() trong nhóm POI lọc theo FinishedUtc IS NULL,
    /// sắp xếp theo TicketId tăng dần. Trả (-1, total) nếu ticket không còn active.
    /// </summary>
    private async Task<(int position, int total)> ComputePositionAsync(
        int poiId, long ticketId, CancellationToken ct)
    {
        var activeIds = await _ctx.QueueTickets.AsNoTracking()
            .Where(t => t.PoiId == poiId && t.FinishedUtc == null)
            .OrderBy(t => t.TicketId)
            .Select(t => t.TicketId)
            .ToListAsync(ct);

        var idx = activeIds.IndexOf(ticketId);
        return idx < 0 ? (-1, activeIds.Count) : (idx + 1, activeIds.Count);
    }
}

public sealed record JoinResult(long TicketId, int Position, int Total);
public sealed record StatusResult(long TicketId, int Position, int Total, bool IsPlaying);

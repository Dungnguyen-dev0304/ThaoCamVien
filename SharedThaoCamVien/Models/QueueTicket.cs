using System;
using System.ComponentModel.DataAnnotations;

namespace SharedThaoCamVien.Models;

/// <summary>
/// Hàng đợi FIFO khi nhiều người cùng quét 1 QR/POI: server gán thứ tự
/// bằng IDENTITY (TicketId tăng dần) — ai INSERT trước có TicketId nhỏ hơn
/// → được phát audio trước.
///
/// Vòng đời 1 ticket:
///   1. Người dùng quét QR → POST /queue/join → row mới, FinishedUtc = NULL
///   2. App poll /queue/status/{id} mỗi 1s, đọc Position
///   3. Khi Position == 1: server set StartedPlayingUtc, app phát audio
///   4. App phát xong → DELETE /queue/{id} → set FinishedUtc
/// </summary>
public partial class QueueTicket
{
    [Key]
    public long TicketId { get; set; }

    public int PoiId { get; set; }

    [StringLength(64)]
    public string SessionId { get; set; } = "";

    public DateTime JoinedUtc { get; set; }

    /// <summary>NULL khi chưa đến lượt; được set khi Position chạm 1.</summary>
    public DateTime? StartedPlayingUtc { get; set; }

    /// <summary>NULL = đang trong hàng / đang phát; có giá trị = đã xong.</summary>
    public DateTime? FinishedUtc { get; set; }
}

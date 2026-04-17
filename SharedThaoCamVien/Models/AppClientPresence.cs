using System;
using System.ComponentModel.DataAnnotations;

namespace SharedThaoCamVien.Models;

/// <summary>
/// One row per app install session; updated by mobile heartbeat when online.
/// </summary>
public partial class AppClientPresence
{
    [Key]
    [StringLength(64)]
    public string SessionId { get; set; } = "";

    public DateTime LastSeenUtc { get; set; }

    public int? CurrentPoiId { get; set; }
}

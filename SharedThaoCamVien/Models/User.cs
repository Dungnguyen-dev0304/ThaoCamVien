using System;
using System.Collections.Generic;

namespace SharedThaoCamVien.Models;

public partial class User
{
    public int UserId { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<PoiVisitHistory> PoiVisitHistories { get; set; } = new List<PoiVisitHistory>();

    public virtual ICollection<UserLocationLog> UserLocationLogs { get; set; } = new List<UserLocationLog>();
}

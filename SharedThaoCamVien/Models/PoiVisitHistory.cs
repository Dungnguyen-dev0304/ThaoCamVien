using System;
using System.Collections.Generic;

namespace SharedThaoCamVien.Models;

public partial class PoiVisitHistory
{
    public long VisitId { get; set; }

    public int? UserId { get; set; }

    public int? PoiId { get; set; }

    public DateTime? VisitTime { get; set; }

    public int? ListenDuration { get; set; }

    [SQLite.Ignore] // <--- Thêm dòng này
    public virtual Poi? Poi { get; set; }

    [SQLite.Ignore] // <--- Thêm dòng này
    public virtual User? User { get; set; }
}

using System;
using System.Collections.Generic;

namespace ApiThaoCamVien.Models;

public partial class Tour
{
    public int TourId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public int? EstimatedTime { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<TourPoi> TourPois { get; set; } = new List<TourPoi>();
}

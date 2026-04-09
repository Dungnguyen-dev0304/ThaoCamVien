using System;
using System.Collections.Generic;

namespace SharedThaoCamVien.Models;

public partial class PoiCategory
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    [SQLite.Ignore]
    public virtual ICollection<Poi> Pois { get; set; } = new List<Poi>();
}

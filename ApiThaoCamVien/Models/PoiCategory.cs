using System;
using System.Collections.Generic;

namespace ApiThaoCamVien.Models;

public partial class PoiCategory
{
    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Poi> Pois { get; set; } = new List<Poi>();
}

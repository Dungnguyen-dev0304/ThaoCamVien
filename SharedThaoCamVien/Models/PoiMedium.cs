using System;
using System.Collections.Generic;

namespace SharedThaoCamVien.Models;

public partial class PoiMedium
{
    public int MediaId { get; set; }

    public int PoiId { get; set; }

    public string MediaType { get; set; } = null!;

    public string MediaUrl { get; set; } = null!;

    public string? Language { get; set; }
    //[SQLite.Ignore]
    public virtual Poi Poi { get; set; } = null!;
}

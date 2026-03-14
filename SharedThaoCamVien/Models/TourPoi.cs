using System;
using System.Collections.Generic;

namespace SharedThaoCamVien.Models;

public partial class TourPoi
{
    public int Id { get; set; }

    public int? TourId { get; set; }

    public int? PoiId { get; set; }

    public int? OrderIndex { get; set; }

    public virtual Poi? Poi { get; set; }

    public virtual Tour? Tour { get; set; }
}

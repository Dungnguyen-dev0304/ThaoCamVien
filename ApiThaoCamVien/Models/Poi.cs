using System;
using System.Collections.Generic;

namespace ApiThaoCamVien.Models;

public partial class Poi
{
    public int PoiId { get; set; }

    public int? CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public int? Radius { get; set; }

    public int? Priority { get; set; }

    public string? ImageThumbnail { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual PoiCategory? Category { get; set; }

    public virtual ICollection<PoiMedium> PoiMedia { get; set; } = new List<PoiMedium>();

    public virtual ICollection<PoiVisitHistory> PoiVisitHistories { get; set; } = new List<PoiVisitHistory>();

    public virtual ICollection<QrCode> QrCodes { get; set; } = new List<QrCode>();

    public virtual ICollection<TourPoi> TourPois { get; set; } = new List<TourPoi>();
}

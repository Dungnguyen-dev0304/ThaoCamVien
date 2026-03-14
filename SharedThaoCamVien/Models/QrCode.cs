using System;
using System.Collections.Generic;

namespace SharedThaoCamVien.Models;

public partial class QrCode
{
    public int QrId { get; set; }

    public int PoiId { get; set; }

    public string QrCodeData { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual Poi Poi { get; set; } = null!;
}

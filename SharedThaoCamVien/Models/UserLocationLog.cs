using System;
using System.Collections.Generic;

namespace SharedThaoCamVien.Models;

public partial class UserLocationLog
{
    public long Id { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public DateTime? RecordedAt { get; set; }
}

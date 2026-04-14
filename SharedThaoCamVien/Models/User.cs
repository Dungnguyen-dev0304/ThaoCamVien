using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SharedThaoCamVien.Models;

public partial class User
{
    public int UserId { get; set; }

    [Required]
    public string Email { get; set; }

    [Required]
    public string DisplayName { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [StringLength(50)]
    public string Password { get; set; }

    /// <summary>
    /// 0 = Admin, 1 = User
    /// </summary>
    [Required]
    public int Role { get; set; } = 1; // Mặc định là User

    public virtual ICollection<PoiVisitHistory> PoiVisitHistories { get; set; } = new List<PoiVisitHistory>();

    public virtual ICollection<UserLocationLog> UserLocationLogs { get; set; } = new List<UserLocationLog>();
}
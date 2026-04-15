using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedThaoCamVien.Models
{
    [Table("poi_media")]
    public partial class PoiMedium
    {
        [Key]
        [Column("media_id")]
        public int MediaId { get; set; }

        [Column("poi_id")]
        public int PoiId { get; set; }

        [Column("media_type")]
        [Required]
        public string MediaType { get; set; } = null!;

        [Column("media_url")]
        [Required]
        public string MediaUrl { get; set; } = null!;

        [Column("language")]
        public string? Language { get; set; }

        // Khóa ngoại liên kết ngược lại bảng Poi
        [ForeignKey("PoiId")]
        public virtual Poi Poi { get; set; } = null!;
    }
}
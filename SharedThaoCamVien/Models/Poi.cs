using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedThaoCamVien.Models
{
    [Table("pois")]
    public class Poi
    {
        [Key]
        [Column("poi_id")]
        public int PoiId { get; set; }

        [Column("category_id")]
        public int? CategoryId { get; set; }

        [SQLite.Column("name")]
        public string? Name { get; set; }

        [SQLite.Column("description")]
        [Column("name")]
        [Required]
        public string Name { get; set; } = null!;

        [Column("description")]
        public string? Description { get; set; }

        // Bạn không cần ghi TypeName ở đây vì WebContext đã có decimal(11, 8)
        [Column("latitude")]
        public decimal Latitude { get; set; }

        [Column("longitude")]
        public decimal Longitude { get; set; }

        [Column("radius")]
        public int? Radius { get; set; }

        [Column("priority")]
        public int? Priority { get; set; }

        [SQLite.Column("image_thumbnail")]

        [Column("image_thumbnail")]
        public string? ImageThumbnail { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        // Mã số để người dùng nhập trên App (ví dụ: "001", "002")
        [Column("audio_code")]
        [StringLength(10)]
        public string? AudioCode { get; set; }

        // Kết nối đến danh sách các file âm thanh
        public virtual ICollection<PoiAudio> PoiAudios { get; set; } = new List<PoiAudio>();
    }
}
using System;
// Bỏ dòng "using SQLite;" đi để tránh nhầm lẫn
using SQLite;

namespace SharedThaoCamVien.Models
{
    [SQLite.Table("pois")]
    public class Poi
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement, SQLite.Column("poi_id")]
        public int PoiId { get; set; }

        [SQLite.Column("category_id")]
        public int? CategoryId { get; set; }

        [SQLite.Column("name")]
        public string Name { get; set; }

        [SQLite.Column("description")]
        public string Description { get; set; }

        // Cứ giữ là decimal theo đúng database SQL của bạn
        [SQLite.Column("latitude")]
        public decimal Latitude { get; set; }

        [SQLite.Column("longitude")]
        public decimal Longitude { get; set; }

        [SQLite.Column("radius")]
        public int? Radius { get; set; }

        [SQLite.Column("priority")]
        public int? Priority { get; set; }

        [SQLite.Column("image_thumbnail")]
        public string ImageThumbnail { get; set; }

        [SQLite.Column("is_active")]
        public bool IsActive { get; set; }

        [SQLite.Column("created_at")]
        public DateTime? CreatedAt { get; set; }
    }
}
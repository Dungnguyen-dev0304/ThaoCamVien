using System;

using SQLite;

using System.Collections.Generic; // Cần dòng này để dùng ICollection


namespace SharedThaoCamVien.Models
{
    [SQLite.Table("pois")]
    public class Poi
    {

        [SQLite.PrimaryKey, SQLite.Column("poi_id")]

        //[SQLite.PrimaryKey, SQLite.AutoIncrement, SQLite.Column("poi_id")]
        public int PoiId { get; set; }

        [SQLite.Column("category_id")]
        public int? CategoryId { get; set; }

        [SQLite.Column("name")]
        public string Name { get; set; }

        [SQLite.Column("description")]
        public string Description { get; set; }

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

        // Thêm cột audio_code để người dùng nhập "1", "2"
        [SQLite.Column("audio_code")]
        public string? AudioCode { get; set; }

        // Kết nối đến danh sách các file âm thanh
        // Lưu ý: SQLite thuần sẽ bỏ qua dòng này, nhưng nó cần thiết để bạn dùng chung Model với API
        [SQLite.Ignore]
        public virtual ICollection<PoiAudio> PoiAudios { get; set; } = new List<PoiAudio>();
    }
}


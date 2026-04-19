using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedThaoCamVien.Models
{
    // ─── EF Core mapping (API/Web) ───
    // [Table]/[Column]/[Key]/[ForeignKey] từ System.ComponentModel.DataAnnotations
    // dùng cho SQL Server phía server.
    //
    // ─── SQLite-net mapping (App mobile) ───
    // [SQLite.Table]/[SQLite.Column]/[SQLite.PrimaryKey] dùng cho SQLite local.
    // Property navigation `Poi` bị [SQLite.Ignore] vì SQLite-net không hiểu
    // kiểu tham chiếu Poi → nếu không ignore sẽ throw khi CreateTableAsync,
    // khiến bảng PoiMedium không được tạo (lỗi "no such table: PoiMedium").
    [Table("poi_media")]
    [SQLite.Table("PoiMedium")]
    public partial class PoiMedium
    {
        [Key]
        [Column("media_id")]
        [SQLite.PrimaryKey, SQLite.Column("media_id")]
        public int MediaId { get; set; }

        [Column("poi_id")]
        [SQLite.Column("poi_id")]
        public int PoiId { get; set; }

        [Column("media_type")]
        [Required]
        [SQLite.Column("media_type")]
        public string MediaType { get; set; } = null!;

        [Column("media_url")]
        [Required]
        [SQLite.Column("media_url")]
        public string MediaUrl { get; set; } = null!;

        [Column("language")]
        [SQLite.Column("language")]
        public string? Language { get; set; }

        // Nav property chỉ dùng bên EF Core (server). SQLite bỏ qua.
        [ForeignKey("PoiId")]
        [SQLite.Ignore]
        public virtual Poi Poi { get; set; } = null!;
    }
}
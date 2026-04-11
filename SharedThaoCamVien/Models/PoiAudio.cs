using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedThaoCamVien.Models
{
    public class PoiAudio
    {
        [Key]
        public int AudioId { get; set; }

        [Required]
        public int PoiId { get; set; }

        [Required]
        [StringLength(10)]
        public string LanguageCode { get; set; } = string.Empty;

        /// <summary>Tên file lưu trên server, ví dụ: poi_3_en_20240101.mp3</summary>
        [StringLength(255)]
        public string? FileName { get; set; }

        /// <summary>Đường dẫn tương đối, ví dụ: /audio/pois/poi_3_en.mp3</summary>
        [StringLength(500)]
        public string? FilePath { get; set; }

        /// <summary>Thời lượng tính bằng giây</summary>
        public float? DurationSeconds { get; set; }

        /// <summary>Kích thước file (bytes)</summary>
        public long? FileSizeBytes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("PoiId")]
        public virtual Poi? Poi { get; set; }
    }
}
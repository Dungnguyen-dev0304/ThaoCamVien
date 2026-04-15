using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedThaoCamVien.Models
{
    [Table("poi_translations")]
    public class PoiTranslation
    {
        [Key]
        [Column("translation_id")]
        public int TranslationId { get; set; }

        [Column("poi_id")]
        public int PoiId { get; set; }

        [ForeignKey("PoiId")]
        //[SQLite.Ignore]
        public Poi? Poi { get; set; }

        [Required]
        [StringLength(10)]
        [Column("language_code")]
        public string LanguageCode { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImperialVip.Models
{
    public class Region
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bölge adı zorunludur")]
        [Display(Name = "Bölge Adı")]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Bölge Adı (İngilizce)")]
        [MaxLength(200)]
        public string NameEn { get; set; } = string.Empty;

        [Display(Name = "Açıklama")]
        [MaxLength(1000)]
        public string? Description { get; set; }

        [Display(Name = "Açıklama (İngilizce)")]
        [MaxLength(1000)]
        public string? DescriptionEn { get; set; }

        [Display(Name = "Görsel URL")]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Fiyat zorunludur")]
        [Display(Name = "Fiyat")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 10000, ErrorMessage = "Fiyat 0-10000 arasında olmalıdır")]
        public decimal Price { get; set; }

        [Display(Name = "Para Birimi")]
        [MaxLength(5)]
        public string Currency { get; set; } = "EUR";

        [Display(Name = "Başlangıç Noktası")]
        [MaxLength(200)]
        public string StartPoint { get; set; } = "Antalya Airport";

        [Display(Name = "Başlangıç Noktası (İngilizce)")]
        [MaxLength(200)]
        public string StartPointEn { get; set; } = "Antalya Airport";

        [Display(Name = "Mesafe (KM)")]
        public double DistanceKm { get; set; }

        [Display(Name = "Tahmini Süre (Dakika)")]
        public int EstimatedDurationMinutes { get; set; }

        [Display(Name = "Sıralama")]
        public int SortOrder { get; set; }

        [Display(Name = "Aktif")]
        public int IsActive { get; set; } = 1;

        [NotMapped]
        [Display(Name = "Aktif")]
        public bool IsActiveBool
        {
            get => IsActive == 1;
            set => IsActive = value ? 1 : 0;
        }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}

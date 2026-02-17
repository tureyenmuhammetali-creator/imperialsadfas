using System.ComponentModel.DataAnnotations;

namespace ImperialVip.Models
{
    public class HeroSlide
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Görsel Yolu")]
        public string ImageUrl { get; set; } = string.Empty;

        [Display(Name = "Sıra")]
        public int SortOrder { get; set; }

        [Display(Name = "Aktif")]
        public int IsActive { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

using System.ComponentModel.DataAnnotations;

namespace ImperialVip.Models
{
    public class Vehicle
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Araç Adı zorunludur.")]
        [Display(Name = "Araç Adı")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Araç Tipi")]
        public string Type { get; set; } = string.Empty; // Sedan, SUV, VAN, Limuzin

        [Display(Name = "Marka")]
        public string Brand { get; set; } = string.Empty;

        [Display(Name = "Model")]
        public string Model { get; set; } = string.Empty;

        [Display(Name = "Yolcu Kapasitesi")]
        public int? PassengerCapacity { get; set; }

        [Display(Name = "Bagaj Kapasitesi")]
        public int? LuggageCapacity { get; set; }

        [Display(Name = "Açıklama")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Özellikler")]
        public string Features { get; set; } = string.Empty; // Deri koltuk, Klima, WiFi vs.

        [Display(Name = "Görsel URL")]
        public string ImageUrl { get; set; } = string.Empty;

        [Display(Name = "KM Başına Fiyat (EUR) - Kullanılmıyor")]
        public decimal PricePerKm { get; set; }

        [Display(Name = "Kullanım ücreti (EUR)")]
        public decimal MinimumPrice { get; set; }

        [Display(Name = "KM Başına Fiyat (USD) - Kullanılmıyor")]
        public decimal PricePerKmUsd { get; set; }

        [Display(Name = "Kullanım ücreti (USD)")]
        public decimal MinimumPriceUsd { get; set; }

        [Display(Name = "KM Başına Fiyat (TRY) - Kullanılmıyor")]
        public decimal PricePerKmTry { get; set; }

        [Display(Name = "Kullanım ücreti (TRY)")]
        public decimal MinimumPriceTry { get; set; }

        [Display(Name = "Para Birimi (varsayılan gösterim)")]
        [MaxLength(5)]
        public string Currency { get; set; } = "EUR";

        [Display(Name = "Aktif")]
        public int? IsActive { get; set; } = 1;

        [Display(Name = "Sıralama")]
        public int? SortOrder { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property for multiple images
        public ICollection<VehicleImage> Images { get; set; } = new List<VehicleImage>();
    }
}

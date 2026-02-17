using System.ComponentModel.DataAnnotations;

namespace ImperialVip.Models
{
    /// <summary>
    /// Ana para birimi EUR. 1 EUR = Rate (ör. TRY için 38.27).
    /// Admin panelinden güncellenir; kur oynadığında admin buradan düzenler.
    /// </summary>
    public class CurrencyRate
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(5)]
        public string CurrencyCode { get; set; } = string.Empty; // TRY, USD, GBP

        [Required]
        public decimal Rate { get; set; } // 1 EUR = Rate

        public DateTime? UpdatedAt { get; set; }
    }
}

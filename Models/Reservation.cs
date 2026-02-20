using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImperialVip.Models
{
    public enum ReservationStatus
    {
        Beklemede = 0,
        Onaylandi = 1,
        Tamamlandi = 2,
        IptalEdildi = 3
    }

    public enum LocationType
    {
        Havalimani = 0,
        Otel = 1,
        Adres = 2
    }

    public class Reservation
    {
        public int Id { get; set; }

        // Müşteri Bilgileri
        [Required(ErrorMessage = "Ad Soyad zorunludur")]
        [Display(Name = "Ad Soyad")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Telefon numarası zorunludur")]
        [Display(Name = "Telefon")]
        [MaxLength(30)]
        public string CustomerPhone { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "E-posta")]
        public string? CustomerEmail { get; set; }

        // Alınacak Nokta
        [Display(Name = "Alınacak Nokta Türü")]
        public LocationType? PickupLocationType { get; set; }

        [Required(ErrorMessage = "Alınacak nokta giriniz")]
        [Display(Name = "Alınacak Nokta")]
        public string PickupLocation { get; set; } = string.Empty;

        [Display(Name = "Alınacak Nokta Detay")]
        public string? PickupLocationDetail { get; set; }

        // Bırakılacak Nokta
        [Display(Name = "Bırakılacak Nokta Türü")]
        public LocationType? DropoffLocationType { get; set; }

        [Required(ErrorMessage = "Bırakılacak nokta giriniz")]
        [Display(Name = "Bırakılacak Nokta")]
        public string DropoffLocation { get; set; } = string.Empty;

        [Display(Name = "Bırakılacak Nokta Detay")]
        public string? DropoffLocationDetail { get; set; }

        // Tarih ve Saat (nullable: eski kayıtlarda NULL olabilir; yeni kayıtta controller'da kontrol edilir)
        [Display(Name = "Transfer Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? TransferDate { get; set; }

        [Display(Name = "Transfer Saati")]
        public string? TransferTime { get; set; } = string.Empty;

        // Bölge (nullable: eski kayıtlarda NULL olabilir; yeni kayıtta controller'da kontrol edilir)
        [Display(Name = "Bölge")]
        public int? RegionId { get; set; }

        [ForeignKey("RegionId")]
        public Region? Region { get; set; }

        // Uçuş ve Havayolu
        [Display(Name = "Uçuş Kodu")]
        public string? FlightNumber { get; set; }

        [Display(Name = "Havayolu Şirketi")]
        [MaxLength(100)]
        public string? AirlineCompany { get; set; }

        [Display(Name = "Otel Adı")]
        [MaxLength(200)]
        public string? HotelName { get; set; }

        [Display(Name = "Dönüş Transferi")]
        public bool IsReturnTransfer { get; set; }

        [Display(Name = "Dönüş Tarihi")]
        [DataType(DataType.Date)]
        public DateTime? ReturnTransferDate { get; set; }

        [Display(Name = "Dönüş Saati")]
        [MaxLength(5)]
        public string? ReturnTransferTime { get; set; }

        [Display(Name = "Dönüş Uçuş Numarası")]
        [MaxLength(20)]
        public string? ReturnFlightNumber { get; set; }

        // Yolcu Bilgisi (nullable: eski kayıtlarda NULL olabilir)
        [Display(Name = "Yolcu Sayısı")]
        [Range(1, 20, ErrorMessage = "Yolcu sayısı 1-20 arasında olmalıdır")]
        public int? PassengerCount { get; set; } = 1;

        [Display(Name = "Yetişkin Sayısı")]
        [Range(1, 20)]
        public int? NumberOfAdults { get; set; } = 1;

        [Display(Name = "Çocuk Sayısı")]
        [Range(0, 20)]
        public int? NumberOfChildren { get; set; } = 0;

        [Display(Name = "Çocuk Koltuğu Sayısı")]
        [Range(0, 10)]
        public int? ChildSeatCount { get; set; } = 0;

        [Display(Name = "Bagaj Sayısı")]
        [Range(0, 20)]
        public int? LuggageCount { get; set; } = 0;

        [Display(Name = "Çocuk İsimleri")]
        [MaxLength(500)]
        public string? ChildNames { get; set; }

        [Display(Name = "Rezervasyon Dili")]
        [MaxLength(5)]
        public string? Language { get; set; } = "en";

        // Araç (nullable: eski kayıtlarda NULL olabilir; yeni rezervasyonda zorunlu)
        [Range(1, int.MaxValue, ErrorMessage = "Lütfen bir araç seçiniz")]
        [Display(Name = "Seçilen Araç")]
        public int? VehicleId { get; set; }

        [ForeignKey("VehicleId")]
        public Vehicle? Vehicle { get; set; }

        /// <summary>2. ve sonraki yolcular için sadece Ad Soyad (örn: "Ahmet Yılmaz; Ayşe Kaya")</summary>
        [Display(Name = "Diğer Yolcu İsimleri")]
        [MaxLength(500)]
        public string? AdditionalPassengerNames { get; set; }

        // Mesafe ve Fiyat (nullable: eski kayıtlarda NULL olabilir)
        [Display(Name = "Mesafe (KM)")]
        public double? DistanceKm { get; set; }

        [Display(Name = "Tahmini Fiyat")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedPrice { get; set; }

        [Display(Name = "Para Birimi")]
        [MaxLength(5)]
        public string? Currency { get; set; } = "EUR";

        // Notlar
        [Display(Name = "Özel Notlar")]
        public string? Notes { get; set; }

        // Durum (nullable: eski kayıtlarda NULL olabilir)
        [Display(Name = "Durum")]
        public ReservationStatus? Status { get; set; } = ReservationStatus.Beklemede;

        [Display(Name = "Admin Notu")]
        public string? AdminNotes { get; set; }

        // Tarih Bilgileri (CreatedAt nullable: eski kayıtlarda NULL olabilir)
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
    }
}

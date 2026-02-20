namespace ImperialVip.Models;

/// <summary>
/// Admin paneli rezervasyon listeleme ekranı için hafif view model.
/// Eski kayıtlardaki NULL sütunlar yüzünden tam <see cref="Reservation"/> entity'si
/// materialize edilirken oluşan "ordinal NULL" hatalarını engellemek için
/// sadece ihtiyaç duyulan alanlar projekte edilir.
/// </summary>
public class AdminReservationListItem
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime? TransferDate { get; set; }
    public string TransferTime { get; set; } = string.Empty;
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string? VehicleName { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public decimal? EstimatedPrice { get; set; }
}


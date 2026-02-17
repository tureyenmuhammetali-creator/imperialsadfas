namespace ImperialVip.Models;

/// <summary>
/// Dashboard'da son rezervasyonlar listesi için kullanılır.
/// Tam Reservation entity yüklenmez; NULL ordinal hatası önlenir.
/// </summary>
public class AdminReservationSummary
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public DateTime? TransferDate { get; set; }
    public string TransferTime { get; set; } = "";
    public string PickupLocation { get; set; } = "";
    public string DropoffLocation { get; set; } = "";
    public ReservationStatus Status { get; set; }
}

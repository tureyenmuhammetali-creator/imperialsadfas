using ImperialVip.Models;

namespace ImperialVip.Services
{
    public interface IReservationPdfService
    {
        byte[] GenerateReservationPdf(Reservation reservation, string lang = "tr");
    }
}

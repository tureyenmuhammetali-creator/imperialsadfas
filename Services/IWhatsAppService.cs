using ImperialVip.Models;

namespace ImperialVip.Services
{
    public interface IWhatsAppService
    {
        Task<bool> SendReservationPdfAsync(Reservation reservation);
    }
}

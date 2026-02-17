using ImperialVip.Models;

namespace ImperialVip.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task<bool> SendReservationConfirmationAsync(Reservation reservation);
        Task<bool> SendReservationNotificationToAdminAsync(Reservation reservation);
        Task<bool> SendContactFormEmailAsync(string name, string email, string phone, string message);
    }
}


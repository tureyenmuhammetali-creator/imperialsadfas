using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using ImperialVip.Models;
using ImperialVip.Infrastructure;

namespace ImperialVip.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;
        private readonly IReservationPdfService _pdfService;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger, IReservationPdfService pdfService)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
            _pdfService = pdfService;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder();
                if (isHtml)
                {
                    builder.HtmlBody = body;
                }
                else
                {
                    builder.TextBody = body;
                }
                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(
                    _emailSettings.SmtpServer,
                    _emailSettings.SmtpPort,
                    _emailSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None
                );
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"✅ Email sent successfully to {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to send email to {to}: {ex.Message}");
                EmailLogHelper.Write($"[SMTP HATA] Alıcı: {to} | Hata: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendEmailWithAttachmentAsync(string to, string subject, string htmlBody, byte[] attachmentBytes, string attachmentFileName)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = htmlBody };
                builder.Attachments.Add(attachmentFileName, attachmentBytes, ContentType.Parse("application/pdf"));

                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(
                    _emailSettings.SmtpServer,
                    _emailSettings.SmtpPort,
                    _emailSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None
                );
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"✅ Email with PDF attachment sent successfully to {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to send email with attachment to {to}: {ex.Message}");
                EmailLogHelper.Write($"[SMTP HATA] Alıcı: {to} | PDF eki ile mail - {ex.Message}");
                return false;
            }
        }

        private byte[]? LoadLogoBytes()
        {
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
            return File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;
        }

        private async Task<bool> SendEmailWithInlineLogoAsync(string to, string subject, string htmlBody, byte[]? logoBytes, byte[]? pdfBytes = null, string? pdfFileName = null)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = htmlBody };

                if (logoBytes != null)
                {
                    var logoResource = builder.LinkedResources.Add("logo.png", logoBytes, ContentType.Parse("image/png"));
                    logoResource.ContentId = "imperial_logo";
                    logoResource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                }

                if (pdfBytes != null && !string.IsNullOrEmpty(pdfFileName))
                    builder.Attachments.Add(pdfFileName, pdfBytes, ContentType.Parse("application/pdf"));

                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, _emailSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Email with inline logo sent to {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email with inline logo to {to}: {ex.Message}");
                EmailLogHelper.Write($"[SMTP HATA] Alıcı: {to} | Inline logo mail - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendEmailWithInlineLogoAndMultipleAttachmentsAsync(string to, string subject, string htmlBody, byte[]? logoBytes, List<(byte[] Data, string FileName)> attachments)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = htmlBody };

                if (logoBytes != null)
                {
                    var logoResource = builder.LinkedResources.Add("logo.png", logoBytes, ContentType.Parse("image/png"));
                    logoResource.ContentId = "imperial_logo";
                    logoResource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                }

                foreach (var att in attachments)
                    builder.Attachments.Add(att.FileName, att.Data, ContentType.Parse("application/pdf"));

                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, _emailSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Email with inline logo and {attachments.Count} attachments sent to {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email with attachments to {to}: {ex.Message}");
                EmailLogHelper.Write($"[SMTP HATA] Alıcı: {to} | Multi-PDF + logo - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SendEmailWithMultipleAttachmentsAsync(string to, string subject, string htmlBody, List<(byte[] Data, string FileName)> attachments)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var builder = new BodyBuilder { HtmlBody = htmlBody };
                foreach (var att in attachments)
                    builder.Attachments.Add(att.FileName, att.Data, ContentType.Parse("application/pdf"));

                message.Body = builder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, _emailSettings.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation($"Email with {attachments.Count} PDF attachments sent to {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email with attachments to {to}: {ex.Message}");
                EmailLogHelper.Write($"[SMTP HATA] Alıcı: {to} | Multi-PDF - {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendReservationConfirmationAsync(Reservation reservation)
        {
            if (string.IsNullOrEmpty(reservation.CustomerEmail))
            {
                _logger.LogWarning("Customer email is empty, skipping confirmation email");
                return false;
            }

            var customerLang = reservation.Language ?? "en";
            var subject = GetSubjectByLang(reservation.Id, customerLang);
            var body = GetReservationConfirmationTemplate(reservation, customerLang);
            var logoBytes = LoadLogoBytes();

            try
            {
                var customerPdf = _pdfService.GenerateReservationPdf(reservation, customerLang);
                var customerPdfName = $"Rezervasyon_{reservation.Id}_{customerLang}.pdf";
                return await SendEmailWithInlineLogoAsync(reservation.CustomerEmail, subject, body, logoBytes, customerPdf, customerPdfName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"PDF oluşturulamadı, e-posta eksiz gönderiliyor: {ex.Message}");
                return await SendEmailWithInlineLogoAsync(reservation.CustomerEmail, subject, body, logoBytes);
            }
        }

        public async Task<bool> SendReservationNotificationToAdminAsync(Reservation reservation)
        {
            var subject = $"🚗 Yeni Rezervasyon - #{reservation.Id} | {reservation.CustomerName}";
            var body = GetAdminNotificationTemplate(reservation);

            var adminEmails = _emailSettings.AdminEmail
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
            if (adminEmails.Count == 0) adminEmails.Add(_emailSettings.AdminEmail);

            var customerLang = reservation.Language ?? "en";
            var attachments = new List<(byte[] Data, string FileName)>();

            try
            {
                var trPdf = _pdfService.GenerateReservationPdf(reservation, "tr");
                attachments.Add((trPdf, $"Rezervasyon_{reservation.Id}_tr.pdf"));

                if (customerLang != "tr")
                {
                    var langPdf = _pdfService.GenerateReservationPdf(reservation, customerLang);
                    attachments.Add((langPdf, $"Rezervasyon_{reservation.Id}_{customerLang}.pdf"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin mail PDF oluşturma hatası: {Message}", ex.Message);
            }

            var allOk = true;
            foreach (var to in adminEmails)
            {
                bool ok;
                if (attachments.Count > 0)
                    ok = await SendEmailWithMultipleAttachmentsAsync(to, subject, body, attachments);
                else
                    ok = await SendEmailAsync(to, subject, body);
                if (!ok) allOk = false;
            }
            return allOk;
        }

        public async Task<bool> SendReservationNotificationToAdminWithLogoAsync(Reservation reservation)
        {
            var subject = $"🚗 Yeni Rezervasyon - #{reservation.Id} | {reservation.CustomerName}";
            var body = GetAdminNotificationTemplate(reservation);
            var logoBytes = LoadLogoBytes();

            var adminEmails = _emailSettings.AdminEmail
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
            if (adminEmails.Count == 0) adminEmails.Add(_emailSettings.AdminEmail);

            var customerLang = reservation.Language ?? "en";
            var attachments = new List<(byte[] Data, string FileName)>();

            try
            {
                var trPdf = _pdfService.GenerateReservationPdf(reservation, "tr");
                attachments.Add((trPdf, $"Rezervasyon_{reservation.Id}_tr.pdf"));

                if (customerLang != "tr")
                {
                    var langPdf = _pdfService.GenerateReservationPdf(reservation, customerLang);
                    attachments.Add((langPdf, $"Rezervasyon_{reservation.Id}_{customerLang}.pdf"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin mail PDF oluşturma hatası: {Message}", ex.Message);
            }

            var allOk = true;
            foreach (var to in adminEmails)
            {
                bool ok;
                if (attachments.Count > 0)
                    ok = await SendEmailWithInlineLogoAndMultipleAttachmentsAsync(to, subject, body, logoBytes, attachments);
                else
                    ok = await SendEmailWithInlineLogoAsync(to, subject, body, logoBytes);
                if (!ok) allOk = false;
            }
            return allOk;
        }

        public async Task<bool> SendContactFormEmailAsync(string name, string email, string phone, string message)
        {
            var subject = $"📩 Yeni İletişim Formu - {name} | Imperial VIP";
            var body = GetContactFormTemplate(name, email, phone, message);

            // Admin adreslerine gönder (virgülle ayrılmış liste)
            var adminEmails = _emailSettings.AdminEmail
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToList();
            if (adminEmails.Count == 0) adminEmails.Add(_emailSettings.AdminEmail);
            var adminResult = true;
            foreach (var to in adminEmails)
            {
                var ok = await SendEmailAsync(to, subject, body);
                if (!ok) adminResult = false;
            }

            // Müşteriye otomatik yanıt gönder
            if (!string.IsNullOrEmpty(email))
            {
                var autoReplySubject = "Mesajınız Alındı - Imperial VIP Transfer";
                var autoReplyBody = GetContactAutoReplyTemplate(name);
                await SendEmailAsync(email, autoReplySubject, autoReplyBody);
            }

            return adminResult;
        }

        private static string GetSubjectByLang(int id, string lang)
        {
            return lang switch
            {
                "de" => $"Reservierungsbestätigung - #{id} | Imperial VIP Transfer",
                "ru" => $"Подтверждение бронирования - #{id} | Imperial VIP Transfer",
                "en" => $"Reservation Confirmation - #{id} | Imperial VIP Transfer",
                _ => $"Rezervasyon Onayı - #{id} | Imperial VIP Transfer"
            };
        }

        private static Dictionary<string, Dictionary<string, string>> GetEmailTranslations()
        {
            return new Dictionary<string, Dictionary<string, string>>
            {
                ["tr"] = new()
                {
                    ["ArrivalInfo"] = "Geliş Bilgileri",
                    ["ReturnInfo"] = "Dönüş Bilgileri",
                    ["FullName"] = "Adı Soyadı",
                    ["Phone"] = "Telefon",
                    ["Email"] = "E-posta",
                    ["PickupPoint"] = "Alış Noktası",
                    ["DropoffPoint"] = "Varış Noktası",
                    ["ArrivalDate"] = "Geliş Tarihi",
                    ["ArrivalFlight"] = "Geliş Uçuş Numarası",
                    ["Airline"] = "Havayolu Şirketi",
                    ["HotelName"] = "Otel Adı",
                    ["Passengers"] = "Yolcular",
                    ["VehicleType"] = "Araç Türü",
                    ["Price"] = "Fiyat",
                    ["Adults"] = "Yetişkin Sayısı",
                    ["Children"] = "Çocuk Sayısı",
                    ["ChildSeats"] = "Çocuk Koltuğu Sayısı",
                    ["SpecialNote"] = "Özel Not",
                    ["ReturnDate"] = "Dönüş Tarihi",
                    ["ReturnFlight"] = "Dönüş Uçuş Numarası",
                    ["PickupTime"] = "Araç Alış Saati"
                },
                ["en"] = new()
                {
                    ["ArrivalInfo"] = "Arrival Information",
                    ["ReturnInfo"] = "Return Information",
                    ["FullName"] = "Full Name",
                    ["Phone"] = "Phone",
                    ["Email"] = "Email",
                    ["PickupPoint"] = "Pick-up Point",
                    ["DropoffPoint"] = "Drop-off Point",
                    ["ArrivalDate"] = "Arrival Date",
                    ["ArrivalFlight"] = "Arrival Flight Number",
                    ["Airline"] = "Airline",
                    ["HotelName"] = "Hotel Name",
                    ["Passengers"] = "Passengers",
                    ["VehicleType"] = "Vehicle Type",
                    ["Price"] = "Price",
                    ["Adults"] = "Number of Adults",
                    ["Children"] = "Number of Children",
                    ["ChildSeats"] = "Child Seats",
                    ["SpecialNote"] = "Special Note",
                    ["ReturnDate"] = "Return Date",
                    ["ReturnFlight"] = "Return Flight Number",
                    ["PickupTime"] = "Pick-up Time"
                },
                ["de"] = new()
                {
                    ["ArrivalInfo"] = "Ankunftsinformationen",
                    ["ReturnInfo"] = "Rückreiseinformationen",
                    ["FullName"] = "Vollständiger Name",
                    ["Phone"] = "Telefon",
                    ["Email"] = "E-Mail",
                    ["PickupPoint"] = "Abholort",
                    ["DropoffPoint"] = "Zielort",
                    ["ArrivalDate"] = "Ankunftsdatum",
                    ["ArrivalFlight"] = "Ankunftsflugnummer",
                    ["Airline"] = "Fluggesellschaft",
                    ["HotelName"] = "Hotelname",
                    ["Passengers"] = "Passagiere",
                    ["VehicleType"] = "Fahrzeugtyp",
                    ["Price"] = "Preis",
                    ["Adults"] = "Anzahl Erwachsene",
                    ["Children"] = "Anzahl Kinder",
                    ["ChildSeats"] = "Kindersitze",
                    ["SpecialNote"] = "Besondere Hinweise",
                    ["ReturnDate"] = "Rückreisedatum",
                    ["ReturnFlight"] = "Rückflugnummer",
                    ["PickupTime"] = "Abholzeit"
                },
                ["ru"] = new()
                {
                    ["ArrivalInfo"] = "Информация о прибытии",
                    ["ReturnInfo"] = "Информация об обратном трансфере",
                    ["FullName"] = "ФИО",
                    ["Phone"] = "Телефон",
                    ["Email"] = "Эл. почта",
                    ["PickupPoint"] = "Место посадки",
                    ["DropoffPoint"] = "Место высадки",
                    ["ArrivalDate"] = "Дата прибытия",
                    ["ArrivalFlight"] = "Номер рейса прибытия",
                    ["Airline"] = "Авиакомпания",
                    ["HotelName"] = "Название отеля",
                    ["Passengers"] = "Пассажиры",
                    ["VehicleType"] = "Тип автомобиля",
                    ["Price"] = "Цена",
                    ["Adults"] = "Взрослых",
                    ["Children"] = "Детей",
                    ["ChildSeats"] = "Детских кресел",
                    ["SpecialNote"] = "Особые пожелания",
                    ["ReturnDate"] = "Дата обратного трансфера",
                    ["ReturnFlight"] = "Номер обратного рейса",
                    ["PickupTime"] = "Время посадки"
                }
            };
        }

        private static string EmailRow(string label, string value)
        {
            return $@"<tr>
<td style='padding:7px 0;font-size:13px;font-weight:bold;color:#000;border-bottom:1px solid #eee;width:40%;'>{label}</td>
<td style='padding:7px 0;font-size:13px;color:#000;border-bottom:1px solid #eee;'>{value}</td>
</tr>";
        }

        private string GetReservationConfirmationTemplate(Reservation reservation, string lang = "tr")
        {
            var translations = GetEmailTranslations();
            var t = translations.ContainsKey(lang) ? translations[lang] : translations["tr"];

            var vehicleName = reservation.Vehicle?.Name ?? "-";
            var yolcular = string.IsNullOrEmpty(reservation.AdditionalPassengerNames)
                ? reservation.CustomerName
                : $"{reservation.CustomerName}, {reservation.AdditionalPassengerNames}";
            var currencySymbol = (reservation.Currency ?? "EUR").ToUpper() switch
            {
                "USD" => "$",
                "TRY" => "₺",
                "GBP" => "£",
                _ => "€"
            };
            var fiyat = reservation.EstimatedPrice.HasValue && reservation.EstimatedPrice.Value > 0
                ? $"{(int)reservation.EstimatedPrice.Value} {currencySymbol}"
                : $"0 {currencySymbol}";
            var yetiskin = reservation.NumberOfAdults ?? reservation.PassengerCount ?? 1;
            var cocuk = reservation.NumberOfChildren ?? 0;
            var cocukKoltuk = reservation.ChildSeatCount ?? 0;
            var ozelNot = string.IsNullOrWhiteSpace(reservation.Notes) ? "-" : reservation.Notes.Trim();
            var gelisTarihi = $"{reservation.TransferDate?.ToString("dd.MM.yyyy") ?? "-"} {reservation.TransferTime}";

            var isGidisDonus = (reservation.IsReturnTransfer == true) && reservation.ReturnTransferDate.HasValue;
            var donusTarihiStr = "";
            var donusUcusNo = reservation.ReturnFlightNumber ?? "-";
            var aracAlisSaati = reservation.ReturnTransferTime ?? "-";
            if (isGidisDonus)
            {
                donusTarihiStr = reservation.ReturnTransferDate!.Value.ToString("dd.MM.yyyy");
                if (!string.IsNullOrEmpty(reservation.ReturnTransferTime))
                    donusTarihiStr += " " + reservation.ReturnTransferTime;
            }

            var footerTel = isGidisDonus ? "+90 532 580 70 77" : "+90 533 925 10 20";

            var emailVal = string.IsNullOrEmpty(reservation.CustomerEmail)
                ? "-"
                : $"<a href='mailto:{reservation.CustomerEmail}' style='color:#1a56db;text-decoration:none;'>{reservation.CustomerEmail}</a>";

            var arrivalRows =
                EmailRow(t["FullName"], reservation.CustomerName) +
                EmailRow(t["Phone"], reservation.CustomerPhone) +
                EmailRow(t["Email"], emailVal) +
                EmailRow(t["PickupPoint"], reservation.PickupLocation) +
                EmailRow(t["DropoffPoint"], reservation.DropoffLocation) +
                EmailRow(t["ArrivalDate"], gelisTarihi) +
                EmailRow(t["ArrivalFlight"], reservation.FlightNumber ?? "-") +
                EmailRow(t["Airline"], reservation.AirlineCompany ?? "-") +
                EmailRow(t["HotelName"], reservation.HotelName ?? "-") +
                EmailRow(t["Passengers"], yolcular) +
                EmailRow(t["VehicleType"], vehicleName) +
                EmailRow(t["Price"], fiyat) +
                EmailRow(t["Adults"], yetiskin.ToString()) +
                EmailRow(t["Children"], cocuk.ToString()) +
                EmailRow(t["ChildSeats"], cocukKoltuk.ToString()) +
                EmailRow(t["SpecialNote"], ozelNot);

            var returnSection = "";
            if (isGidisDonus)
            {
                returnSection = $@"
<tr><td style='padding:24px 30px 0 30px;'>
<p style='font-size:16px;font-weight:bold;margin:0 0 12px 0;color:#000;'>{t["ReturnInfo"]}</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='font-family:Arial,Helvetica,sans-serif;'>
{EmailRow(t["ReturnDate"], donusTarihiStr)}
{EmailRow(t["ReturnFlight"], donusUcusNo)}
{EmailRow(t["PickupTime"], aracAlisSaati)}
</table>
</td></tr>";
            }

            return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1.0'>
</head>
<body style='margin:0;padding:0;background:#f4f4f4;font-family:Arial,Helvetica,sans-serif;'>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='max-width:600px;margin:0 auto;background:#ffffff;'>
<tr><td style='background:#1B1B3A;padding:18px 30px;text-align:center;'>
<img src='cid:imperial_logo' alt='Imperial VIP' height='50' style='height:50px;display:block;margin:0 auto;' />
</td></tr>
<tr><td style='padding:24px 30px 0 30px;'>
<p style='font-size:16px;font-weight:bold;margin:0 0 12px 0;color:#000;'>{t["ArrivalInfo"]}</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='font-family:Arial,Helvetica,sans-serif;'>
{arrivalRows}
</table>
</td></tr>
{returnSection}
<tr><td style='background:#1B1B3A;padding:16px 30px;text-align:center;'>
<img src='cid:imperial_logo' alt='Imperial VIP' height='28' style='height:28px;display:block;margin:0 auto 8px auto;' />
<p style='color:#ffffff;font-size:12px;margin:0;'>Imperial VIP Transfer - 2026</p>
<p style='color:#ffffff;font-size:11px;margin:4px 0 0 0;'>info@transferimperialvip.com &bull; {footerTel}</p>
</td></tr>
</table>
</body>
</html>";
        }

        private string GetAdminNotificationTemplate(Reservation reservation)
        {
            var vehicleName = reservation.Vehicle?.Name ?? "Belirtilmedi";
            var pickupType = (reservation.PickupLocationType ?? LocationType.Havalimani) == LocationType.Havalimani ? "✈️ Havalimanı" : "🏨 Otel";
            var dropoffType = (reservation.DropoffLocationType ?? LocationType.Havalimani) == LocationType.Havalimani ? "✈️ Havalimanı" : "🏨 Otel";
            
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #0f172a 0%, #1e3a5f 100%); padding: 24px; text-align: center; border-radius: 10px 10px 0 0;'>
        <h1 style='color: #d4af37; margin: 0; font-size: 24px;'>🚗 YENİ REZERVASYON</h1>
        <p style='color: #94a3b8; margin: 8px 0 0 0;'>Rezervasyon No: #{reservation.Id}</p>
    </div>
    
    <div style='background: #ffffff; padding: 25px; border: 1px solid #e2e8f0; border-top: none;'>
        <h2 style='color: #0f172a; margin: 0 0 15px 0; border-bottom: 2px solid #d4af37; padding-bottom: 8px;'>👤 Müşteri Bilgileri</h2>
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
            <tr style='background: #f8fafc;'>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Ad Soyad:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>{reservation.CustomerName}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Telefon:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>
                    <a href='tel:{reservation.CustomerPhone}' style='color: #1e3a5f; font-weight: bold;'>{reservation.CustomerPhone}</a>
                </td>
            </tr>
            <tr style='background: #f8fafc;'>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>E-posta:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>{reservation.CustomerEmail ?? "Belirtilmedi"}</td>
            </tr>
        </table>
        
        <h2 style='color: #0f172a; margin: 0 0 15px 0; border-bottom: 2px solid #d4af37; padding-bottom: 8px;'>📍 Transfer Bilgileri</h2>
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
            <tr style='background: #f8fafc;'>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Tarih & Saat:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0; font-size: 16px; font-weight: bold; color: #0f172a;'>{reservation.TransferDate?.ToString("dd.MM.yyyy") ?? "-"} - {reservation.TransferTime}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Alınacak Nokta:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>{pickupType}<br><strong>{reservation.PickupLocation}</strong><br><em>{reservation.PickupLocationDetail}</em></td>
            </tr>
            <tr style='background: #f8fafc;'>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Bırakılacak Nokta:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>{dropoffType}<br><strong>{reservation.DropoffLocation}</strong><br><em>{reservation.DropoffLocationDetail}</em></td>
            </tr>
            <tr>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Uçuş Kodu:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>{reservation.FlightNumber ?? "Belirtilmedi"}</td>
            </tr>
        </table>
        
        <h2 style='color: #0f172a; margin: 0 0 15px 0; border-bottom: 2px solid #d4af37; padding-bottom: 8px;'>🚙 Araç & Yolcu</h2>
        <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
            <tr style='background: #f8fafc;'>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Tercih Edilen Araç:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>{vehicleName}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Yolcu Sayısı:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>{reservation.PassengerCount} kişi</td>
            </tr>
            <tr style='background: #f8fafc;'>
                <td style='padding: 10px; border: 1px solid #e2e8f0; color: #64748b;'><strong>Bagaj Sayısı:</strong></td>
                <td style='padding: 10px; border: 1px solid #e2e8f0;'>{reservation.LuggageCount} adet</td>
            </tr>
        </table>
        
        {(string.IsNullOrEmpty(reservation.Notes) ? "" : $@"
        <div style='background: #fef9e7; padding: 15px; border-radius: 5px; border-left: 4px solid #d4af37;'>
            <strong>📝 Müşteri Notu:</strong><br>
            <span style='color: #64748b;'>{reservation.Notes}</span>
        </div>
        ")}
        
        <div style='margin-top: 20px; text-align: center;'>
            <a href='https://wa.me/{(reservation.CustomerPhone ?? "").Replace(" ", "").Replace("+", "")}' style='display: inline-block; background: #25d366; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; font-weight: bold;'>📱 WhatsApp ile İletişime Geç</a>
        </div>
    </div>
    
    <div style='background: #0f172a; padding: 15px; text-align: center; border-radius: 0 0 10px 10px;'>
        <p style='color: #94a3b8; margin: 0; font-size: 12px;'>
            Oluşturulma: {reservation.CreatedAt?.ToString("dd.MM.yyyy HH:mm") ?? "-"} | Imperial VIP Transfer
        </p>
    </div>
</body>
</html>";
        }

        private string GetContactFormTemplate(string name, string email, string phone, string message)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: #1e40af; padding: 20px; text-align: center; border-radius: 10px 10px 0 0;'>
        <h1 style='color: white; margin: 0;'>📩 Yeni İletişim Formu</h1>
    </div>
    
    <div style='background: #ffffff; padding: 25px; border: 1px solid #e2e8f0;'>
        <table style='width: 100%; border-collapse: collapse;'>
            <tr style='background: #eff6ff;'>
                <td style='padding: 12px; border: 1px solid #bfdbfe;'><strong>👤 Ad Soyad:</strong></td>
                <td style='padding: 12px; border: 1px solid #bfdbfe;'>{name}</td>
            </tr>
            <tr>
                <td style='padding: 12px; border: 1px solid #bfdbfe;'><strong>📧 E-posta:</strong></td>
                <td style='padding: 12px; border: 1px solid #bfdbfe;'><a href='mailto:{email}'>{email}</a></td>
            </tr>
            <tr style='background: #eff6ff;'>
                <td style='padding: 12px; border: 1px solid #bfdbfe;'><strong>📞 Telefon:</strong></td>
                <td style='padding: 12px; border: 1px solid #bfdbfe;'><a href='tel:{phone}'>{phone}</a></td>
            </tr>
        </table>
        
        <div style='margin-top: 20px; padding: 15px; background: #f8fafc; border-left: 4px solid #1e40af; border-radius: 4px;'>
            <strong>💬 Mesaj:</strong>
            <p style='margin: 10px 0 0 0; white-space: pre-wrap;'>{message}</p>
        </div>
        
        <div style='margin-top: 20px; text-align: center;'>
            <a href='mailto:{email}' style='display: inline-block; background: #1e40af; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin: 5px;'>📧 E-posta Yanıtla</a>
            <a href='tel:{phone}' style='display: inline-block; background: #059669; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin: 5px;'>📞 Telefon Et</a>
        </div>
    </div>
    
    <div style='background: #374151; padding: 15px; text-align: center; border-radius: 0 0 10px 10px;'>
        <p style='color: #9ca3af; margin: 0; font-size: 12px;'>
            Gönderilme: {DateTime.Now:dd.MM.yyyy HH:mm} | Imperial VIP Web Sitesi
        </p>
    </div>
</body>
</html>";
        }

        private string GetContactAutoReplyTemplate(string name)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background: linear-gradient(135deg, #0f172a 0%, #1e3a5f 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
        <h1 style='color: #d4af37; margin: 0;'>IMPERIAL <span style='color: white;'>VIP</span></h1>
    </div>
    
    <div style='background: #ffffff; padding: 30px; border: 1px solid #e2e8f0;'>
        <h2 style='color: #0f172a;'>Sayın {name},</h2>
        
        <p>Mesajınız başarıyla tarafımıza ulaştı. En kısa sürede sizinle iletişime geçeceğiz.</p>
        
        <p>Acil durumlar için bize doğrudan ulaşabilirsiniz:</p>
        
        <div style='text-align: center; margin: 25px 0;'>
            <a href='tel:+905339251020' style='display: inline-block; background: #0f172a; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin: 5px;'>📞 +90 533 925 10 20</a>
            <a href='https://wa.me/905339251020' style='display: inline-block; background: #25d366; color: white; padding: 12px 25px; text-decoration: none; border-radius: 5px; margin: 5px;'>💬 WhatsApp</a>
        </div>
        
        <p>Teşekkür ederiz,<br><strong>Imperial VIP Transfer Ekibi</strong></p>
    </div>
    
    <div style='background: #0f172a; padding: 20px; text-align: center; border-radius: 0 0 10px 10px;'>
        <p style='color: #94a3b8; margin: 0; font-size: 12px;'>
            © 2024 Imperial VIP Transfer. Tüm hakları saklıdır.
        </p>
    </div>
</body>
</html>";
        }
    }
}

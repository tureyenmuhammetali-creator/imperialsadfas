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

        public async Task<bool> SendReservationConfirmationAsync(Reservation reservation)
        {
            if (string.IsNullOrEmpty(reservation.CustomerEmail))
            {
                _logger.LogWarning("Customer email is empty, skipping confirmation email");
                return false;
            }

            var subject = $"Rezervasyon Onayı - #{reservation.Id} | Imperial VIP Transfer";
            var body = GetReservationConfirmationTemplate(reservation);

            try
            {
                var pdfBytes = _pdfService.GenerateReservationPdf(reservation);
                var pdfName = $"Rezervasyon_{reservation.Id}_tr.pdf";
                return await SendEmailWithAttachmentAsync(reservation.CustomerEmail, subject, body, pdfBytes, pdfName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"PDF oluşturulamadı, e-posta ekinden gönderiliyor: {ex.Message}");
                return await SendEmailAsync(reservation.CustomerEmail, subject, body);
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

            var allOk = true;
            foreach (var to in adminEmails)
            {
                var ok = await SendEmailAsync(to, subject, body);
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

        private string GetReservationConfirmationTemplate(Reservation reservation)
        {
            var vehicleName = reservation.Vehicle?.Name ?? "Belirtilmedi";
            var yolcular = string.IsNullOrEmpty(reservation.AdditionalPassengerNames)
                ? reservation.CustomerName
                : $"{reservation.CustomerName}, {reservation.AdditionalPassengerNames}";
            var fiyat = reservation.EstimatedPrice.HasValue && reservation.EstimatedPrice.Value > 0
                ? $"{(int)reservation.EstimatedPrice.Value} €"
                : "0 €";
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

            // PDF'deki gibi: Gidiş-dönüşte +90 532 580 70 77, tek seferde +90 533 925 10 20
            var footerTel = isGidisDonus ? "+90 532 580 70 77" : "+90 533 925 10 20";

            // Header - PDF ve görsellerdeki gibi: koyu lacivert banner, altın IMPERIALVIP logo
            var headerImgUrl = !string.IsNullOrWhiteSpace(_emailSettings.SiteBaseUrl)
                ? $"{_emailSettings.SiteBaseUrl.TrimEnd('/')}/images/imgs/imperial-page-header.png"
                : "";
            var headerHtml = !string.IsNullOrEmpty(headerImgUrl)
                ? $@"<div style='text-align: center; margin: 0 0 24px 0;'><img src='{headerImgUrl}' alt='Imperial VIP Transfer' width='560' height='80' style='max-width: 100%; height: auto; display: block; margin: 0 auto;' /></div>"
                : @"<div style='background: #0a0e1a; padding: 28px 20px; text-align: center; margin: 0 0 24px 0;'>
<span style='font-family: Georgia, serif; font-size: 28px; font-weight: bold; color: #d4af37; letter-spacing: 2px;'>IMPERIAL</span><span style='font-family: Georgia, serif; font-size: 28px; font-weight: bold; color: #c0c0c0; letter-spacing: 2px;'>VIP</span>
</div>";

            // Geliş Bilgileri tablosu - PDF sırası ve ifadeleri birebir (Rezervasyon_5358_tr.pdf, Rezervasyon_5363_tr.pdf)
            var gelisTable = $@"
<table style='width: 100%; border-collapse: collapse; font-size: 14px; color: #000; font-family: Arial, Helvetica, sans-serif;'>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Adı Soyadı</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{reservation.CustomerName}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Telefon</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{reservation.CustomerPhone}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>E-posta</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{(string.IsNullOrEmpty(reservation.CustomerEmail) ? "-" : $"<a href='mailto:{reservation.CustomerEmail}' style='color: #1e40af;'>{reservation.CustomerEmail}</a>")}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Alış Noktası</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{reservation.PickupLocation}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Varış Noktası</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{reservation.DropoffLocation}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Geliş Tarihi</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{gelisTarihi}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Geliş Uçuş Numarası</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{reservation.FlightNumber ?? "-"}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Havayolu Şirketi</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{reservation.AirlineCompany ?? "-"}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Otel Adı</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{reservation.HotelName ?? "-"}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Yolcular</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{yolcular}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Araç Türü</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{vehicleName}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Fiyat</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{fiyat}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Yetişkin Sayısı</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{yetiskin}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Çocuk Sayısı</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{cocuk}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Çocuk Koltuğu Sayısı</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{cocukKoltuk}</td></tr>
<tr><td style='padding: 6px 0;'><strong>Özel Not</strong></td><td style='padding: 6px 0; text-align: right;'>{ozelNot}</td></tr>
</table>";

            var donusTable = "";
            var sayfaGosterge = "-- 1 of 1 --";
            if (isGidisDonus)
            {
                sayfaGosterge = "-- 1 of 2 --";
                donusTable = $@"
<p style='margin: 28px 0 12px 0; font-size: 16px; font-weight: bold; color: #000;'>Dönüş Bilgileri</p>
<table style='width: 100%; border-collapse: collapse; font-size: 14px; color: #000; font-family: Arial, Helvetica, sans-serif;'>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Dönüş Tarihi</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{donusTarihiStr}</td></tr>
<tr><td style='padding: 6px 0; border-bottom: 1px solid #eee;'><strong>Dönüş Uçuş Numarası</strong></td><td style='padding: 6px 0; text-align: right; border-bottom: 1px solid #eee;'>{donusUcusNo}</td></tr>
<tr><td style='padding: 6px 0;'><strong>Araç Alış Saati</strong></td><td style='padding: 6px 0; text-align: right;'>{aracAlisSaati}</td></tr>
</table>";
            }

            // Footer - PDF ve görsellerdeki gibi: koyu lacivert, Imperial VIP Transfer - 2026, iletişim bilgileri
            var footerImgUrl = !string.IsNullOrWhiteSpace(_emailSettings.SiteBaseUrl)
                ? $"{_emailSettings.SiteBaseUrl.TrimEnd('/')}/images/imgs/imperial-page-footer.png"
                : "";
            var footerBlock = !string.IsNullOrEmpty(footerImgUrl)
                ? $@"<div style='text-align: center; margin: 28px 0 0 0;'><img src='{footerImgUrl}' alt='Imperial VIP Transfer' width='560' height='120' style='max-width: 100%; height: auto; display: block; margin: 0 auto;' /></div>"
                : $@"<div style='background: #0a0e1a; padding: 24px 20px; text-align: center; margin: 28px 0 0 0;'>
<p style='margin: 0; font-size: 18px; font-weight: bold; color: #d4af37; letter-spacing: 1px;'>Imperial VIP Transfer - 2026</p>
<p style='margin: 8px 0 0 0; font-size: 14px; color: #fff;'>info@transferimperialvip.com • {footerTel}</p>
</div>";

            var donusFooter = "";
            if (isGidisDonus)
            {
                donusFooter = $@"
<p style='margin: 24px 0 0 0; font-size: 13px; color: #000;'>Imperial VIP Transfer - 2026</p>
<p style='margin: 4px 0 0 0; font-size: 13px; color: #000;'>info@transferimperialvip.com • {footerTel}</p>
<p style='text-align: center; margin: 16px 0 0 0; font-size: 12px; color: #94a3b8;'>-- 2 of 2 --</p>";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1.0'>
<title>Rezervasyon Onayı - #{reservation.Id} | Imperial VIP Transfer</title>
</head>
<body style='font-family: Arial, Helvetica, sans-serif; font-size: 14px; line-height: 1.5; color: #000; max-width: 600px; margin: 0 auto; padding: 0; background: #fff;'>
<div style='max-width: 600px; margin: 0 auto; padding: 24px;'>
{headerHtml}
<p style='margin: 0 0 16px 0; font-size: 16px; font-weight: bold; color: #0a0e1a;'>Geliş Bilgileri</p>
{gelisTable}
<p style='margin: 20px 0 0 0; font-size: 13px; color: #000;'>Imperial VIP Transfer - 2026</p>
<p style='margin: 4px 0 0 0; font-size: 13px; color: #000;'>info@transferimperialvip.com • {footerTel}</p>
<p style='text-align: center; margin: 20px 0 0 0; font-size: 12px; color: #94a3b8;'>{sayfaGosterge}</p>
{donusTable}
{donusFooter}
{(!isGidisDonus ? footerBlock : "")}
</div>
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

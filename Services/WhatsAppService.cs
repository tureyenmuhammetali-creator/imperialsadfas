using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ImperialVip.Models;
using ImperialVip.Infrastructure;

namespace ImperialVip.Services
{
    public class WhatsAppService : IWhatsAppService
    {
        private readonly WhatsAppSettings _settings;
        private readonly IReservationPdfService _pdfService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WhatsAppService> _logger;

        public WhatsAppService(
            IOptions<WhatsAppSettings> settings,
            IReservationPdfService pdfService,
            IHttpClientFactory httpClientFactory,
            ILogger<WhatsAppService> logger)
        {
            _settings = settings.Value;
            _pdfService = pdfService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> SendReservationPdfAsync(Reservation reservation)
        {
            if (!_settings.Enabled)
            {
                _logger.LogInformation("WhatsApp entegrasyonu devre dışı, mesaj gönderilmedi.");
                return false;
            }

            if (string.IsNullOrEmpty(_settings.AccessToken) ||
                string.IsNullOrEmpty(_settings.PhoneNumberId) ||
                string.IsNullOrEmpty(_settings.RecipientPhone))
            {
                _logger.LogWarning("WhatsApp ayarları eksik (AccessToken, PhoneNumberId veya RecipientPhone).");
                return false;
            }

            try
            {
                var customerLang = reservation.Language ?? "en";
                var caption = BuildCaption(reservation);
                var allOk = true;

                var trPdf = _pdfService.GenerateReservationPdf(reservation, "tr");
                var trFileName = $"Rezervasyon_{reservation.Id}_tr.pdf";
                var trMediaId = await UploadMediaAsync(trPdf, trFileName);
                if (!string.IsNullOrEmpty(trMediaId))
                {
                    var sent = await SendDocumentMessageAsync(trMediaId, trFileName, caption);
                    if (sent)
                        _logger.LogInformation("Rezervasyon #{Id} WhatsApp TR PDF gönderildi.", reservation.Id);
                    else
                        allOk = false;
                }
                else
                {
                    _logger.LogError("WhatsApp TR media yüklenemedi.");
                    allOk = false;
                }

                if (customerLang != "tr")
                {
                    var langPdf = _pdfService.GenerateReservationPdf(reservation, customerLang);
                    var langFileName = $"Rezervasyon_{reservation.Id}_{customerLang}.pdf";
                    var langMediaId = await UploadMediaAsync(langPdf, langFileName);
                    if (!string.IsNullOrEmpty(langMediaId))
                    {
                        var sent = await SendDocumentMessageAsync(langMediaId, langFileName, $"📄 {customerLang.ToUpper()} - Reservation #{reservation.Id}");
                        if (sent)
                            _logger.LogInformation("Rezervasyon #{Id} WhatsApp {Lang} PDF gönderildi.", reservation.Id, customerLang.ToUpper());
                        else
                            allOk = false;
                    }
                    else
                    {
                        _logger.LogError("WhatsApp {Lang} media yüklenemedi.", customerLang.ToUpper());
                        allOk = false;
                    }
                }

                if (allOk)
                    EmailLogHelper.Write($"[WHATSAPP] Rezervasyon #{reservation.Id}: PDF'ler gönderildi -> {_settings.RecipientPhone}");

                return allOk;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rezervasyon #{Id} WhatsApp gönderiminde hata: {Message}", reservation.Id, ex.Message);
                EmailLogHelper.Write($"[WHATSAPP HATA] Rezervasyon #{reservation.Id}: {ex.Message}");
                return false;
            }
        }

        private async Task<string?> UploadMediaAsync(byte[] fileBytes, string fileName)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{_settings.PhoneNumberId}/media";

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(fileContent, "file", fileName);
            content.Add(new StringContent("application/pdf"), "type");
            content.Add(new StringContent("whatsapp"), "messaging_product");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

            var response = await client.PostAsync(url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("WhatsApp media upload başarısız: {Status} - {Body}", response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("id", out var idEl))
                return idEl.GetString();

            _logger.LogError("WhatsApp media upload yanıtında id bulunamadı: {Body}", body);
            return null;
        }

        private async Task<bool> SendDocumentMessageAsync(string mediaId, string fileName, string caption)
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/{_settings.ApiVersion}/{_settings.PhoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                to = _settings.RecipientPhone,
                type = "document",
                document = new
                {
                    id = mediaId,
                    filename = fileName,
                    caption
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

            var response = await client.PostAsync(url, requestContent);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("WhatsApp mesaj gönderimi başarısız: {Status} - {Body}", response.StatusCode, body);
                return false;
            }

            _logger.LogInformation("WhatsApp mesaj gönderildi: {Body}", body);
            return true;
        }

        private static string BuildCaption(Reservation reservation)
        {
            var isGidisDonus = reservation.IsReturnTransfer == true && reservation.ReturnTransferDate.HasValue;
            var transferTipi = isGidisDonus ? "Gidiş-Dönüş" : "Tek Yön";
            var tarih = reservation.TransferDate?.ToString("dd.MM.yyyy") ?? "-";
            var saat = reservation.TransferTime ?? "";
            var currencySymbol = (reservation.Currency ?? "EUR").ToUpper() switch
            {
                "USD" => "$",
                "TRY" => "₺",
                "GBP" => "£",
                _ => "€"
            };
            var fiyat = reservation.EstimatedPrice.HasValue && reservation.EstimatedPrice.Value > 0
                ? $"{(int)reservation.EstimatedPrice.Value} {currencySymbol}"
                : "-";

            var sb = new StringBuilder();
            sb.AppendLine($"🚗 Yeni Rezervasyon #{reservation.Id}");
            sb.AppendLine($"👤 {reservation.CustomerName}");
            sb.AppendLine($"📞 {reservation.CustomerPhone}");
            sb.AppendLine($"📍 {reservation.PickupLocation} → {reservation.DropoffLocation}");
            sb.AppendLine($"📅 {tarih} {saat}");
            sb.AppendLine($"🔄 {transferTipi}");
            sb.AppendLine($"💰 {fiyat}");
            if (!string.IsNullOrWhiteSpace(reservation.Vehicle?.Name))
                sb.AppendLine($"🚙 {reservation.Vehicle.Name}");

            return sb.ToString().TrimEnd();
        }
    }
}

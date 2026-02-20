namespace ImperialVip.Models
{
    public class WhatsAppSettings
    {
        /// <summary>Meta WhatsApp Cloud API erişim tokeni</summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>Meta uygulamasındaki WhatsApp telefon numarası ID'si</summary>
        public string PhoneNumberId { get; set; } = string.Empty;

        /// <summary>PDF gönderilecek alıcı WhatsApp numarası (ör: 905325807077)</summary>
        public string RecipientPhone { get; set; } = string.Empty;

        /// <summary>WhatsApp entegrasyonunu aç/kapat</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>Graph API versiyonu (ör: v21.0)</summary>
        public string ApiVersion { get; set; } = "v22.0";
    }
}

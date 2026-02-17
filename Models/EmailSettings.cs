namespace ImperialVip.Models
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
        /// <summary>E-postalardaki logo için site adresi (örn: https://transferimperialvip.com)</summary>
        public string SiteBaseUrl { get; set; } = string.Empty;
    }
}


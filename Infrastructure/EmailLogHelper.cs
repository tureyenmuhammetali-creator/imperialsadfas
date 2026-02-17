namespace ImperialVip.Infrastructure;

/// <summary>
/// Rezervasyon ve e-posta olaylarını Logs/email_log.txt dosyasına yazar.
/// Log dosyasını açarak ne olduğunu görebilirsiniz.
/// </summary>
public static class EmailLogHelper
{
    private static readonly object _lock = new();
    private static string? _logFolder;

    public static void SetLogFolder(string contentRootPath)
    {
        if (_logFolder == null)
            _logFolder = Path.Combine(contentRootPath, "Logs");
    }

    public static void Write(string message)
    {
        try
        {
            if (string.IsNullOrEmpty(_logFolder)) return;
            Directory.CreateDirectory(_logFolder);
            var path = Path.Combine(_logFolder, "email_log.txt");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(path, line);
            }
        }
        catch { /* log yazma hatası sessizce geç */ }
    }
}

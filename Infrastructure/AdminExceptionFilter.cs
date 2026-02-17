using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImperialVip.Infrastructure
{
    /// <summary>
    /// Admin panelindeki tüm action'larda oluşan veritabanı ve genel hataları yakalar,
    /// kullanıcıya anlaşılır Türkçe hata mesajı gösterir (TempData toast popup).
    /// </summary>
    public class AdminExceptionFilter : IExceptionFilter
    {
        private readonly ILogger<AdminExceptionFilter> _logger;
        private readonly ITempDataDictionaryFactory _tempDataFactory;

        public AdminExceptionFilter(ILogger<AdminExceptionFilter> logger, ITempDataDictionaryFactory tempDataFactory)
        {
            _logger = logger;
            _tempDataFactory = tempDataFactory;
        }

        public void OnException(ExceptionContext context)
        {
            var errorMessage = ParseErrorMessage(context.Exception);

            _logger.LogError(context.Exception,
                "Admin işleminde hata: {Action} - {Message}",
                context.ActionDescriptor.DisplayName,
                errorMessage);

            var tempData = _tempDataFactory.GetTempData(context.HttpContext);
            tempData["Error"] = errorMessage;

            // POST isteklerinde Referer'a yönlendir, yoksa admin ana sayfasına
            var referer = context.HttpContext.Request.Headers["Referer"].ToString();
            if (context.HttpContext.Request.Method == "POST" && !string.IsNullOrEmpty(referer))
            {
                context.Result = new RedirectResult(referer);
            }
            else
            {
                context.Result = new RedirectToActionResult("Index", "Admin", null);
            }

            context.ExceptionHandled = true;
        }

        private static string ParseErrorMessage(Exception ex)
        {
            // En içteki exception'ı bul
            var innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;

            // SQLite NOT NULL hatası
            if (innermost is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
            {
                var msg = sqliteEx.Message;
                if (msg.Contains("NOT NULL constraint failed"))
                {
                    var field = msg.Split(':').LastOrDefault()?.Trim() ?? "Bilinmeyen alan";
                    return $"Veritabanı hatası: '{field}' alanı boş bırakılamaz. Lütfen tüm zorunlu alanları doldurunuz.";
                }
                if (msg.Contains("UNIQUE constraint failed"))
                {
                    var field = msg.Split(':').LastOrDefault()?.Trim() ?? "Bilinmeyen alan";
                    return $"Veritabanı hatası: '{field}' alanında zaten aynı değere sahip bir kayıt mevcut.";
                }
                if (msg.Contains("FOREIGN KEY constraint failed"))
                {
                    return "Veritabanı hatası: İlişkili kayıt bulunamadı. Seçilen araç, bölge veya ilişkili veri geçersiz olabilir.";
                }
                return $"Veritabanı hatası: {msg}";
            }

            // EF Core DbUpdateException
            if (ex is DbUpdateException)
            {
                return $"Veritabanı kayıt hatası: {innermost.Message}";
            }

            // Genel hata
            return $"Beklenmeyen bir hata oluştu: {innermost.Message}";
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ImperialVip.Data;
using ImperialVip.Models;

namespace ImperialVip.Services
{
    public class CurrencyRateService : ICurrencyRateService
    {
        private const string CacheKey = "Imperial_CurrencyRates";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public CurrencyRateService(ApplicationDbContext context, IConfiguration configuration, IMemoryCache cache)
        {
            _context = context;
            _configuration = configuration;
            _cache = cache;
        }

        public IReadOnlyDictionary<string, decimal> GetRates()
        {
            return _cache.GetOrCreate(CacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                entry.Size = 1; // SizeLimit kullanıldığında her giriş için Size gerekli
                var fromDb = _context.CurrencyRates.AsNoTracking()
                    .ToDictionary(x => x.CurrencyCode, x => x.Rate);

                var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EUR"] = 1m
                };

                foreach (var code in new[] { "TRY", "USD", "GBP" })
                {
                    if (fromDb.TryGetValue(code, out var rate))
                        result[code] = rate;
                    else
                    {
                        var configRate = _configuration.GetValue<decimal>($"CurrencyRates:{code}", code == "TRY" ? 38.27m : code == "USD" ? 1.05m : 0.83m);
                        result[code] = configRate;
                    }
                }

                return (IReadOnlyDictionary<string, decimal>)result;
            })!;
        }

        public async Task SaveRatesAsync(decimal tryRate, decimal usdRate, decimal gbpRate)
        {
            var now = DateTime.UtcNow;
            var codes = new[] { ("TRY", tryRate), ("USD", usdRate), ("GBP", gbpRate) };

            foreach (var (code, rate) in codes)
            {
                // AsTracking GEREKLİ: DbContext varsayılan NoTracking kullanıyor; tracked olmadan SaveChanges değişiklikleri kaydetmez
                var entity = await _context.CurrencyRates.AsTracking().FirstOrDefaultAsync(c => c.CurrencyCode == code);
                if (entity != null)
                {
                    entity.Rate = rate;
                    entity.UpdatedAt = now;
                }
                else
                {
                    _context.CurrencyRates.Add(new CurrencyRate
                    {
                        CurrencyCode = code,
                        Rate = rate,
                        UpdatedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync();
            _cache.Remove(CacheKey);
        }
    }
}

namespace ImperialVip.Services
{
    /// <summary>
    /// Ana para birimi EUR. Sitede gösterilen kurlar önce veritabanından okunur;
    /// yoksa appsettings'ten alınır. Admin panelinden güncellenebilir.
    /// </summary>
    public interface ICurrencyRateService
    {
        /// <summary>1 EUR = Rate. Anahtarlar: EUR (1), TRY, USD, GBP.</summary>
        IReadOnlyDictionary<string, decimal> GetRates();
        Task SaveRatesAsync(decimal tryRate, decimal usdRate, decimal gbpRate);
    }
}

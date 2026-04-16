using LocalWallet.Models;
using LocalWallet.Services.Database;

namespace LocalWallet.Services.ExchangeRates;

public class ExchangeRateService : IExchangeRateService
{
    private readonly NbpRatesProvider _nbp;
    private readonly FrankfurterRatesProvider _frankfurter;
    private readonly IDatabaseService _db;

    public ExchangeRateService(
        NbpRatesProvider nbp,
        FrankfurterRatesProvider frankfurter,
        IDatabaseService db)
    {
        _nbp = nbp;
        _frankfurter = frankfurter;
        _db = db;
    }

    public async Task<bool> RefreshRatesAsync(string baseCurrency, CancellationToken ct = default)
    {
        IReadOnlyList<ExchangeRate>? rates = null;

        if (baseCurrency.Equals("PLN", StringComparison.OrdinalIgnoreCase))
        {
            rates = await _nbp.FetchAsync(ct);
        }

        if (rates is null)
        {
            rates = await _frankfurter.FetchAsync(baseCurrency, ct);
        }

        if (rates is null || rates.Count == 0) return false;

        await _db.SaveRatesAsync(rates);

        var settings = await _db.GetSettingsAsync();
        settings.LastRatesUpdate = DateTime.UtcNow;
        await _db.SaveSettingsAsync(settings);

        return true;
    }

    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken ct = default)
    {
        if (fromCurrency.Equals(toCurrency, StringComparison.OrdinalIgnoreCase)) return amount;

        var settings = await _db.GetSettingsAsync();
        var baseCurrency = settings.BaseCurrency;

        var rateFrom = await _db.GetRateAsync(baseCurrency, fromCurrency);
        var rateTo = await _db.GetRateAsync(baseCurrency, toCurrency);

        if (rateFrom is null || rateTo is null || rateFrom.Rate == 0) return amount;

        var inBase = amount / rateFrom.Rate;
        return inBase * rateTo.Rate;
    }

    public async Task<IReadOnlyList<ExchangeRate>> GetCachedRatesAsync(string baseCurrency)
    {
        return await _db.GetLatestRatesAsync(baseCurrency);
    }

    public async Task<DateTime?> GetLastUpdateAsync()
    {
        var s = await _db.GetSettingsAsync();
        return s.LastRatesUpdate;
    }
}

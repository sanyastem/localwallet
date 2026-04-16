using LocalWallet.Models;

namespace LocalWallet.Services.ExchangeRates;

public interface IExchangeRateService
{
    Task<bool> RefreshRatesAsync(string baseCurrency, CancellationToken ct = default);
    Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken ct = default);
    Task<IReadOnlyList<ExchangeRate>> GetCachedRatesAsync(string baseCurrency);
    Task<DateTime?> GetLastUpdateAsync();
}

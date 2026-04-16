using LocalWallet.Models;
using LocalWallet.Services.Database;

namespace LocalWallet.Services;

public interface ISettingsService
{
    Task<AppSettings> GetAsync();
    Task SetBaseCurrencyAsync(string currency);
    Task SetDisplayCurrenciesAsync(IEnumerable<string> currencies);
    Task SetBiometricEnabledAsync(bool enabled);
}

public class SettingsService : ISettingsService
{
    private readonly IDatabaseService _db;

    public SettingsService(IDatabaseService db) => _db = db;

    public Task<AppSettings> GetAsync() => _db.GetSettingsAsync();

    public async Task SetBaseCurrencyAsync(string currency)
    {
        var s = await _db.GetSettingsAsync();
        s.BaseCurrency = currency.ToUpperInvariant();
        await _db.SaveSettingsAsync(s);
    }

    public async Task SetDisplayCurrenciesAsync(IEnumerable<string> currencies)
    {
        var s = await _db.GetSettingsAsync();
        s.DisplayCurrenciesCsv = string.Join(",", currencies.Select(c => c.ToUpperInvariant()));
        await _db.SaveSettingsAsync(s);
    }

    public async Task SetBiometricEnabledAsync(bool enabled)
    {
        var s = await _db.GetSettingsAsync();
        s.BiometricEnabled = enabled;
        await _db.SaveSettingsAsync(s);
    }
}

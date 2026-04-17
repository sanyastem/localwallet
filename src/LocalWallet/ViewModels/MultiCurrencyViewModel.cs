using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

public partial class MultiCurrencyViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly IExchangeRateService _rates;

    [ObservableProperty] private ObservableCollection<CurrencyBalance> balances = new();
    [ObservableProperty] private ObservableCollection<RateDisplay> rates = new();
    [ObservableProperty] private string lastUpdatedText = "никогда";
    [ObservableProperty] private bool hasAccounts;
    [ObservableProperty] private bool hasRates;

    public MultiCurrencyViewModel(IDatabaseService db, IExchangeRateService rates)
    {
        _db = db;
        _rates = rates;
        Title = "В нескольких валютах";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _db.GetSettingsAsync();
            var baseCurrency = settings.BaseCurrency;
            var displayCurrencies = settings.DisplayCurrenciesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToUpperInvariant())
                .ToList();
            var displaySet = displayCurrencies.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var accounts = await _db.GetAccountsAsync();
            var transactions = await _db.GetTransactionsAsync();
            HasAccounts = accounts.Count > 0;

            var amountByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var acc in accounts)
                amountByCurrency[acc.Currency] = amountByCurrency.GetValueOrDefault(acc.Currency) + acc.InitialBalance;
            foreach (var t in transactions)
                amountByCurrency[t.Currency] = amountByCurrency.GetValueOrDefault(t.Currency) + t.Amount;

            decimal totalInBase = 0;
            foreach (var (ccy, amount) in amountByCurrency)
            {
                if (amount == 0) continue;
                totalInBase += await _rates.ConvertAsync(amount, ccy, baseCurrency);
            }

            Balances.Clear();
            foreach (var code in displayCurrencies)
            {
                var amount = await _rates.ConvertAsync(totalInBase, baseCurrency, code);
                Balances.Add(new CurrencyBalance { Currency = code, Amount = amount });
            }

            var cached = await _rates.GetCachedRatesAsync(baseCurrency);
            Rates.Clear();
            foreach (var r in cached
                .Where(r => r.TargetCurrency != r.BaseCurrency
                         && displaySet.Contains(r.TargetCurrency))
                .OrderBy(r => r.TargetCurrency))
            {
                Rates.Add(new RateDisplay
                {
                    From = r.BaseCurrency,
                    To = r.TargetCurrency,
                    Rate = r.Rate
                });
            }
            HasRates = Rates.Count > 0;

            var last = await _rates.GetLastUpdateAsync();
            LastUpdatedText = last.HasValue
                ? last.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "никогда";
        }
        catch { /* best-effort — pull-to-refresh never gets stuck */ }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshRatesAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _db.GetSettingsAsync();
            var ok = await _rates.RefreshRatesAsync(settings.BaseCurrency);
            await LoadAsync();
            if (!ok) await UiAlerts.ShowAsync("Курсы", "Не удалось обновить. Проверьте интернет.");
        }
        catch { }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAccountAsync()
    {
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync(nameof(AccountsPage)); }
        catch { }
    }
}

public class CurrencyBalance
{
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AmountFormatted => $"{Amount:0.00} {Currency}";
}

public class RateDisplay
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string Pair => $"1 {From} = {Rate:0.0000} {To}";
}

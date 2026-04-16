using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var settings = await _db.GetSettingsAsync();
            var baseCurrency = settings.BaseCurrency;
            var displayCurrencies = settings.DisplayCurrenciesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var accounts = await _db.GetAccountsAsync();
            var transactions = await _db.GetTransactionsAsync();
            HasAccounts = accounts.Count > 0;

            decimal totalInBase = 0;
            foreach (var acc in accounts)
            {
                totalInBase += await _rates.ConvertAsync(acc.InitialBalance, acc.Currency, baseCurrency);
            }
            foreach (var t in transactions)
            {
                totalInBase += await _rates.ConvertAsync(t.Amount, t.Currency, baseCurrency);
            }

            Balances.Clear();
            foreach (var code in displayCurrencies)
            {
                var amount = await _rates.ConvertAsync(totalInBase, baseCurrency, code);
                Balances.Add(new CurrencyBalance { Currency = code, Amount = amount });
            }

            var cached = await _rates.GetCachedRatesAsync(baseCurrency);
            Rates.Clear();
            foreach (var r in cached.Where(r => r.TargetCurrency != r.BaseCurrency)
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
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshRatesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var settings = await _db.GetSettingsAsync();
            var ok = await _rates.RefreshRatesAsync(settings.BaseCurrency);
            if (!ok && Application.Current?.MainPage is not null)
            {
                await Application.Current.MainPage.DisplayAlert("Курсы", "Не удалось обновить. Проверьте интернет.", "OK");
            }
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAccountAsync()
    {
        await Shell.Current.GoToAsync(nameof(AccountsPage));
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

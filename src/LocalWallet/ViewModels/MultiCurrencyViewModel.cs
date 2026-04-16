using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

public partial class MultiCurrencyViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly IExchangeRateService _rates;

    [ObservableProperty] private ObservableCollection<CurrencyBalance> balances = new();
    [ObservableProperty] private string lastUpdatedText = "никогда";

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
}

public class CurrencyBalance
{
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AmountFormatted => $"{Amount:0.00} {Currency}";
}

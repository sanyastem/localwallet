using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly IExchangeRateService _rates;

    [ObservableProperty] private decimal totalBalance;
    [ObservableProperty] private string baseCurrency = "PLN";
    [ObservableProperty] private ObservableCollection<TransactionDisplay> recentTransactions = new();
    [ObservableProperty] private bool hasAccounts;

    public DashboardViewModel(IDatabaseService db, IExchangeRateService rates)
    {
        _db = db;
        _rates = rates;
        Title = "Главная";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _db.GetSettingsAsync();
            BaseCurrency = settings.BaseCurrency;

            var accounts = await _db.GetAccountsAsync();
            var transactions = await _db.GetTransactionsAsync();
            HasAccounts = accounts.Count > 0;

            decimal total = 0;
            foreach (var acc in accounts)
            {
                total += await _rates.ConvertAsync(acc.InitialBalance, acc.Currency, BaseCurrency);
            }
            foreach (var t in transactions)
            {
                var signed = transactions.FirstOrDefault(x => x.Id == t.Id);
                var account = accounts.FirstOrDefault(a => a.Id == t.AccountId);
                if (account is null) continue;
                var converted = await _rates.ConvertAsync(t.Amount, t.Currency, BaseCurrency);
                total += converted;
            }
            TotalBalance = total;

            var categories = (await _db.GetCategoriesAsync()).ToDictionary(c => c.Id);
            var accountsById = accounts.ToDictionary(a => a.Id);

            RecentTransactions.Clear();
            foreach (var t in transactions.Take(10))
            {
                categories.TryGetValue(t.CategoryId, out var cat);
                accountsById.TryGetValue(t.AccountId, out var acc);
                RecentTransactions.Add(new TransactionDisplay
                {
                    Id = t.Id,
                    Date = t.Date.ToLocalTime(),
                    Amount = t.Amount,
                    Currency = t.Currency,
                    CategoryName = cat?.Name ?? "—",
                    AccountName = acc?.Name ?? "—",
                    IsIncome = cat?.Type == CategoryType.Income,
                    Note = t.Note
                });
            }

        }
        catch { /* keep PTR unstuck */ }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddTransactionAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddTransactionPage));
    }

    [RelayCommand]
    private async Task CreateAccountAsync()
    {
        await Shell.Current.GoToAsync(nameof(AccountsPage));
    }
}

public class TransactionDisplay
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public bool IsIncome { get; set; }
    public string? Note { get; set; }

    public string AmountFormatted => $"{(IsIncome ? "+" : "−")}{Math.Abs(Amount):0.00} {Currency}";
    public Color AmountColor => IsIncome ? Colors.SeaGreen : Color.FromArgb("#D32F2F");
}

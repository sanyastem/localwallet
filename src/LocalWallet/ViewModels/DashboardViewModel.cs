using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services;
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

            var amountByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var acc in accounts)
                amountByCurrency[acc.Currency] = amountByCurrency.GetValueOrDefault(acc.Currency) + acc.InitialBalance;
            var accountIds = accounts.Select(a => a.Id).ToHashSet();
            foreach (var t in transactions)
            {
                if (!accountIds.Contains(t.AccountId)) continue;
                amountByCurrency[t.Currency] = amountByCurrency.GetValueOrDefault(t.Currency) + t.Amount;
            }

            decimal total = 0;
            foreach (var (ccy, amount) in amountByCurrency)
            {
                if (amount == 0) continue;
                total += await _rates.ConvertAsync(amount, ccy, BaseCurrency);
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
    private async Task AddTransactionAsync() => await NavigateAsync(nameof(AddTransactionPage));

    [RelayCommand]
    private async Task CreateAccountAsync() => await NavigateAsync(nameof(AccountsPage));

    private static async Task NavigateAsync(string route)
    {
        try
        {
            if (Shell.Current is null)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard.Navigate] Shell.Current is null for {route}");
                await UiAlerts.ShowAsync("Навигация", $"Не удалось открыть '{route}'.");
                return;
            }
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard.Navigate] {route}: {ex}");
            await UiAlerts.ShowAsync("Ошибка навигации", ex.Message);
        }
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

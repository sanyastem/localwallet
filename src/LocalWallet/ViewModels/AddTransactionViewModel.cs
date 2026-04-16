using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

public partial class AddTransactionViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly IExchangeRateService _rates;

    [ObservableProperty] private ObservableCollection<Account> accounts = new();
    [ObservableProperty] private ObservableCollection<Category> categories = new();
    [ObservableProperty] private ObservableCollection<CurrencyInfo> availableCurrencies = new();
    [ObservableProperty] private Account? selectedAccount;
    [ObservableProperty] private Category? selectedCategory;
    [ObservableProperty] private CurrencyInfo? selectedCurrency;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private DateTime date = DateTime.Now;
    [ObservableProperty] private string? note;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpenseBg))]
    [NotifyPropertyChangedFor(nameof(ExpenseText))]
    [NotifyPropertyChangedFor(nameof(IncomeBg))]
    [NotifyPropertyChangedFor(nameof(IncomeText))]
    private bool isExpense = true;

    public Color ExpenseBg => IsExpense ? Color.FromArgb("#D32F2F") : Colors.Transparent;
    public Color ExpenseText => IsExpense ? Colors.White : Color.FromArgb("#D32F2F");
    public Color IncomeBg => !IsExpense ? Color.FromArgb("#2E7D32") : Colors.Transparent;
    public Color IncomeText => !IsExpense ? Colors.White : Color.FromArgb("#2E7D32");

    [RelayCommand] private void SetExpense() => IsExpense = true;
    [RelayCommand] private void SetIncome() => IsExpense = false;

    public AddTransactionViewModel(IDatabaseService db, IExchangeRateService rates)
    {
        _db = db;
        _rates = rates;
        Title = "Новая операция";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (AvailableCurrencies.Count == 0)
        {
            foreach (var c in SupportedCurrencies.All) AvailableCurrencies.Add(c);
        }

        var accs = await _db.GetAccountsAsync();
        var cats = await _db.GetCategoriesAsync();
        Accounts.Clear();
        foreach (var a in accs) Accounts.Add(a);
        Categories.Clear();
        foreach (var c in cats.Where(c => c.Type == (IsExpense ? CategoryType.Expense : CategoryType.Income)))
            Categories.Add(c);

        SelectedAccount ??= Accounts.FirstOrDefault();
        SelectedCategory ??= Categories.FirstOrDefault();
        if (SelectedAccount is not null)
            SelectedCurrency = SupportedCurrencies.Find(SelectedAccount.Currency) ?? AvailableCurrencies[0];
    }

    partial void OnIsExpenseChanged(bool value)
    {
        _ = LoadAsync();
    }

    partial void OnSelectedAccountChanged(Account? value)
    {
        if (value is not null)
            SelectedCurrency = SupportedCurrencies.Find(value.Currency) ?? SelectedCurrency;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedAccount is null || SelectedCategory is null || SelectedCurrency is null || Amount <= 0)
        {
            if (Application.Current?.MainPage is not null)
                await Application.Current.MainPage.DisplayAlert("Проверка", "Заполните все поля.", "OK");
            return;
        }

        var settings = await _db.GetSettingsAsync();
        var currency = SelectedCurrency.Code;
        var rate = await _rates.ConvertAsync(1m, currency, settings.BaseCurrency);

        var signedAmount = IsExpense ? -Math.Abs(Amount) : Math.Abs(Amount);

        var transaction = new Transaction
        {
            AccountId = SelectedAccount.Id,
            CategoryId = SelectedCategory.Id,
            Amount = signedAmount,
            Currency = currency,
            Date = Date.ToUniversalTime(),
            Note = Note,
            ExchangeRateToBase = rate
        };
        await _db.SaveTransactionAsync(transaction);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private Task CancelAsync() => Shell.Current.GoToAsync("..");
}

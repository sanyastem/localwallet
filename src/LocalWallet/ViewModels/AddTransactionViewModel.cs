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
    [ObservableProperty] private Account? selectedAccount;
    [ObservableProperty] private Category? selectedCategory;
    [ObservableProperty] private decimal amount;
    [ObservableProperty] private string currency = "PLN";
    [ObservableProperty] private DateTime date = DateTime.Now;
    [ObservableProperty] private string? note;
    [ObservableProperty] private bool isExpense = true;

    public AddTransactionViewModel(IDatabaseService db, IExchangeRateService rates)
    {
        _db = db;
        _rates = rates;
        Title = "Новая операция";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var accs = await _db.GetAccountsAsync();
        var cats = await _db.GetCategoriesAsync();
        Accounts.Clear();
        foreach (var a in accs) Accounts.Add(a);
        Categories.Clear();
        foreach (var c in cats.Where(c => c.Type == (IsExpense ? CategoryType.Expense : CategoryType.Income)))
            Categories.Add(c);

        SelectedAccount ??= Accounts.FirstOrDefault();
        SelectedCategory ??= Categories.FirstOrDefault();
        if (SelectedAccount is not null) Currency = SelectedAccount.Currency;
    }

    partial void OnIsExpenseChanged(bool value)
    {
        _ = LoadAsync();
    }

    partial void OnSelectedAccountChanged(Account? value)
    {
        if (value is not null) Currency = value.Currency;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedAccount is null || SelectedCategory is null || Amount <= 0)
        {
            if (Application.Current?.MainPage is not null)
                await Application.Current.MainPage.DisplayAlert("Проверка", "Заполните все поля.", "OK");
            return;
        }

        var settings = await _db.GetSettingsAsync();
        var rate = await _rates.ConvertAsync(1m, Currency, settings.BaseCurrency);

        var signedAmount = IsExpense ? -Math.Abs(Amount) : Math.Abs(Amount);

        var transaction = new Transaction
        {
            AccountId = SelectedAccount.Id,
            CategoryId = SelectedCategory.Id,
            Amount = signedAmount,
            Currency = Currency,
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

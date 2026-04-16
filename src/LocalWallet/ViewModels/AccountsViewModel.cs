using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services.Database;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

public partial class AccountsViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;

    [ObservableProperty] private ObservableCollection<Account> items = new();
    [ObservableProperty] private ObservableCollection<CurrencyInfo> availableCurrencies = new();
    [ObservableProperty] private string newAccountName = string.Empty;
    [ObservableProperty] private CurrencyInfo? selectedCurrency;
    [ObservableProperty] private decimal newAccountInitialBalance;

    public AccountsViewModel(IDatabaseService db)
    {
        _db = db;
        Title = "Счета";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (AvailableCurrencies.Count == 0)
        {
            foreach (var c in SupportedCurrencies.All) AvailableCurrencies.Add(c);
        }

        if (SelectedCurrency is null)
        {
            var settings = await _db.GetSettingsAsync();
            SelectedCurrency = SupportedCurrencies.Find(settings.BaseCurrency) ?? AvailableCurrencies[0];
        }

        var list = await _db.GetAccountsAsync();
        Items.Clear();
        foreach (var a in list) Items.Add(a);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountName) || SelectedCurrency is null) return;
        var account = new Account
        {
            Name = NewAccountName.Trim(),
            Currency = SelectedCurrency.Code,
            InitialBalance = NewAccountInitialBalance
        };
        await _db.SaveAccountAsync(account);
        NewAccountName = string.Empty;
        NewAccountInitialBalance = 0;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(Account account)
    {
        if (account is null) return;
        await _db.DeleteAccountAsync(account.Id);
        Items.Remove(account);
    }
}

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
    [ObservableProperty] private string newAccountName = string.Empty;
    [ObservableProperty] private string newAccountCurrency = "PLN";
    [ObservableProperty] private decimal newAccountInitialBalance;

    public AccountsViewModel(IDatabaseService db)
    {
        _db = db;
        Title = "Счета";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var list = await _db.GetAccountsAsync();
        Items.Clear();
        foreach (var a in list) Items.Add(a);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountName)) return;
        var account = new Account
        {
            Name = NewAccountName.Trim(),
            Currency = NewAccountCurrency.ToUpperInvariant(),
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

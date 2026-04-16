using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services.Database;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

public partial class TransactionsViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;

    [ObservableProperty] private ObservableCollection<TransactionDisplay> items = new();
    [ObservableProperty] private DateTime fromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime toDate = DateTime.Today.AddDays(1).AddSeconds(-1);

    public TransactionsViewModel(IDatabaseService db)
    {
        _db = db;
        Title = "Операции";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var transactions = await _db.GetTransactionsInRangeAsync(FromDate.ToUniversalTime(), ToDate.ToUniversalTime());
            var categories = (await _db.GetCategoriesAsync()).ToDictionary(c => c.Id);
            var accounts = (await _db.GetAccountsAsync()).ToDictionary(a => a.Id);

            Items.Clear();
            foreach (var t in transactions)
            {
                categories.TryGetValue(t.CategoryId, out var cat);
                accounts.TryGetValue(t.AccountId, out var acc);
                Items.Add(new TransactionDisplay
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
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(TransactionDisplay item)
    {
        if (item is null) return;
        await _db.DeleteTransactionAsync(item.Id);
        Items.Remove(item);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        await Shell.Current.GoToAsync(nameof(AddTransactionPage));
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services;
using LocalWallet.Services.Database;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

public partial class TransactionsViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly IEntityWriter _writer;

    [ObservableProperty] private ObservableCollection<TransactionDisplay> items = new();
    [ObservableProperty] private DateTime fromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime toDate = DateTime.Today.AddDays(1).AddSeconds(-1);

    public TransactionsViewModel(IDatabaseService db, IEntityWriter writer)
    {
        _db = db;
        _writer = writer;
        Title = "Операции";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
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
        catch { }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(TransactionDisplay item)
    {
        if (item is null) return;
        try
        {
            var full = await _db.GetTransactionAsync(item.Id);
            if (full is not null) await _writer.DeleteTransactionAsync(full);
            Items.Remove(item);
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        try
        {
            if (Shell.Current is null)
            {
                await UiAlerts.ShowAsync("Навигация", "Не удалось открыть форму операции.");
                return;
            }
            await Shell.Current.GoToAsync(nameof(AddTransactionPage));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Transactions.Add] {ex}");
            await UiAlerts.ShowAsync("Ошибка навигации", ex.Message);
        }
    }
}

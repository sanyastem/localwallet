using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services;
using LocalWallet.Services.Database;
using LocalWallet.Services.Families;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

public partial class AccountsViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly IEntityWriter _writer;
    private readonly IFamilyService _families;

    [ObservableProperty] private ObservableCollection<Account> items = new();
    [ObservableProperty] private ObservableCollection<CurrencyInfo> availableCurrencies = new();
    [ObservableProperty] private ObservableCollection<FamilyScope> availableScopes = new();
    [ObservableProperty] private string newAccountName = string.Empty;
    [ObservableProperty] private CurrencyInfo? selectedCurrency;
    [ObservableProperty] private FamilyScope? selectedScope;
    [ObservableProperty] private decimal newAccountInitialBalance;

    public AccountsViewModel(IDatabaseService db, IEntityWriter writer, IFamilyService families)
    {
        _db = db;
        _writer = writer;
        _families = families;
        Title = "Счета";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            if (AvailableCurrencies.Count == 0)
            {
                foreach (var c in SupportedCurrencies.All) AvailableCurrencies.Add(c);
            }

            AvailableScopes.Clear();
            AvailableScopes.Add(new FamilyScope(null, "Личный"));
            foreach (var f in await _families.ListAsync())
            {
                AvailableScopes.Add(new FamilyScope(f.Id, $"Семья «{f.Name}»"));
            }
            SelectedScope ??= AvailableScopes[0];

            if (SelectedCurrency is null && AvailableCurrencies.Count > 0)
            {
                var settings = await _db.GetSettingsAsync();
                SelectedCurrency = SupportedCurrencies.Find(settings.BaseCurrency) ?? AvailableCurrencies[0];
            }

            var list = await _db.GetAccountsAsync();
            Items.Clear();
            foreach (var a in list) Items.Add(a);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Accounts.Load] {ex}");
            await UiAlerts.ShowAsync("Счета", $"Не удалось загрузить: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(NewAccountName) || SelectedCurrency is null)
        {
            await UiAlerts.ShowAsync("Проверка", "Укажите название и валюту.");
            return;
        }
        IsBusy = true;
        try
        {
            var account = new Account
            {
                Name = NewAccountName.Trim(),
                Currency = SelectedCurrency.Code,
                InitialBalance = NewAccountInitialBalance,
                FamilyId = SelectedScope?.FamilyId
            };
            await _writer.SaveAccountAsync(account);
            NewAccountName = string.Empty;
            NewAccountInitialBalance = 0;
            await LoadAsync();
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync(Account account)
    {
        if (account is null) return;
        try
        {
            await _writer.DeleteAccountAsync(account);
            Items.Remove(account);
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
    }
}

public record FamilyScope(Guid? FamilyId, string Label);

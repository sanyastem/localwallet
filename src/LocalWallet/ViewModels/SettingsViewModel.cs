using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly ISettingsService _settings;
    private readonly IBiometricService _biometric;
    private readonly IExportService _export;
    private readonly IExchangeRateService _rates;
    private readonly IDeviceIdentityService _identity;

    [ObservableProperty] private ObservableCollection<CurrencyInfo> availableCurrencies = new();
    [ObservableProperty] private ObservableCollection<CurrencyChoice> displayChoices = new();
    [ObservableProperty] private CurrencyInfo? selectedBaseCurrency;
    [ObservableProperty] private bool biometricEnabled;
    [ObservableProperty] private bool biometricAvailable;
    [ObservableProperty] private DateTime deleteFromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime deleteToDate = DateTime.Today;
    [ObservableProperty] private string lastRatesUpdateText = "никогда";
    [ObservableProperty] private string deviceDisplayName = "Моё устройство";
    [ObservableProperty] private string deviceIdShort = string.Empty;

    public SettingsViewModel(
        IDatabaseService db,
        ISettingsService settings,
        IBiometricService biometric,
        IExportService export,
        IExchangeRateService rates,
        IDeviceIdentityService identity)
    {
        _db = db;
        _settings = settings;
        _biometric = biometric;
        _export = export;
        _rates = rates;
        _identity = identity;
        Title = "Настройки";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        AvailableCurrencies.Clear();
        foreach (var c in SupportedCurrencies.All) AvailableCurrencies.Add(c);

        var s = await _settings.GetAsync();
        SelectedBaseCurrency = SupportedCurrencies.Find(s.BaseCurrency) ?? AvailableCurrencies.First();

        var selected = s.DisplayCurrenciesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .ToHashSet();

        DisplayChoices.Clear();
        foreach (var c in SupportedCurrencies.All)
        {
            DisplayChoices.Add(new CurrencyChoice
            {
                Currency = c,
                IsSelected = selected.Contains(c.Code)
            });
        }

        BiometricAvailable = await _biometric.IsAvailableAsync();
        BiometricEnabled = s.BiometricEnabled && BiometricAvailable;
        LastRatesUpdateText = s.LastRatesUpdate?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "никогда";

        try { await _identity.InitializeAsync(); } catch { }
        DeviceDisplayName = _identity.DisplayName;
        DeviceIdShort = _identity.DeviceId.Length > 12 ? _identity.DeviceId[..12] + "…" : _identity.DeviceId;
    }

    [RelayCommand]
    private async Task SaveDeviceNameAsync()
    {
        await _identity.SetDisplayNameAsync(DeviceDisplayName);
        if (Application.Current?.MainPage is not null)
            await Application.Current.MainPage.DisplayAlert("Устройство", "Имя сохранено.", "OK");
    }

    [RelayCommand]
    private async Task SaveCurrencyAsync()
    {
        var baseCode = SelectedBaseCurrency?.Code ?? "PLN";
        var selected = DisplayChoices.Where(c => c.IsSelected).Select(c => c.Currency.Code).ToList();
        if (selected.Count == 0) selected.Add(baseCode);
        if (!selected.Contains(baseCode)) selected.Insert(0, baseCode);

        await _settings.SetBaseCurrencyAsync(baseCode);
        await _settings.SetDisplayCurrenciesAsync(selected);

        if (Application.Current?.MainPage is not null)
            await Application.Current.MainPage.DisplayAlert("Настройки", "Сохранено.", "OK");
    }

    [RelayCommand]
    private async Task ToggleBiometricAsync()
    {
        if (BiometricEnabled)
        {
            if (!BiometricAvailable)
            {
                BiometricEnabled = false;
                if (Application.Current?.MainPage is not null)
                    await Application.Current.MainPage.DisplayAlert("Биометрия", "Не настроена на устройстве.", "OK");
                return;
            }
            var ok = await _biometric.AuthenticateAsync("Подтвердите включение биометрии");
            if (!ok)
            {
                BiometricEnabled = false;
                return;
            }
        }
        await _settings.SetBiometricEnabledAsync(BiometricEnabled);
    }

    [RelayCommand]
    private async Task DeletePeriodAsync()
    {
        if (Application.Current?.MainPage is null) return;

        var confirm = await Application.Current.MainPage.DisplayAlert(
            "Удалить операции",
            $"Удалить все операции с {DeleteFromDate:d} по {DeleteToDate:d}? Действие необратимо.",
            "Удалить", "Отмена");
        if (!confirm) return;

        var count = await _db.DeleteTransactionsInRangeAsync(
            DeleteFromDate.ToUniversalTime(),
            DeleteToDate.AddDays(1).AddSeconds(-1).ToUniversalTime());

        await Application.Current.MainPage.DisplayAlert("Готово", $"Удалено: {count}", "OK");
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        try
        {
            var path = await _export.ExportTransactionsCsvAsync();
            await _export.ShareExportAsync(path);
        }
        catch (Exception ex)
        {
            if (Application.Current?.MainPage is not null)
                await Application.Current.MainPage.DisplayAlert("Ошибка", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task RefreshRatesAsync()
    {
        var baseCode = SelectedBaseCurrency?.Code ?? "PLN";
        var ok = await _rates.RefreshRatesAsync(baseCode);
        await LoadAsync();
        if (Application.Current?.MainPage is not null)
            await Application.Current.MainPage.DisplayAlert(
                "Курсы",
                ok ? "Обновлено." : "Не удалось обновить.",
                "OK");
    }

    [RelayCommand]
    private async Task OpenAccountsAsync() => await Shell.Current.GoToAsync(nameof(AccountsPage));

    [RelayCommand]
    private async Task OpenCategoriesAsync() => await Shell.Current.GoToAsync(nameof(CategoriesPage));

    [RelayCommand]
    private async Task OpenFamiliesAsync() => await Shell.Current.GoToAsync(nameof(FamilyListPage));

    [RelayCommand]
    private async Task ResetAllAsync()
    {
        if (Application.Current?.MainPage is null) return;
        var once = await Application.Current.MainPage.DisplayAlert(
            "Сброс", "Удалить все данные приложения? Действие необратимо.", "Продолжить", "Отмена");
        if (!once) return;
        var twice = await Application.Current.MainPage.DisplayAlert(
            "Подтверждение", "Вы уверены? Это удалит все операции, счета и настройки.", "Удалить всё", "Отмена");
        if (!twice) return;

        await _db.ResetAllDataAsync();
        await LoadAsync();
        await Application.Current.MainPage.DisplayAlert("Готово", "Данные сброшены.", "OK");
    }
}

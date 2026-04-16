using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services;
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

    [ObservableProperty] private string baseCurrency = "PLN";
    [ObservableProperty] private string displayCurrencies = "PLN,EUR,USD";
    [ObservableProperty] private bool biometricEnabled;
    [ObservableProperty] private DateTime deleteFromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime deleteToDate = DateTime.Today;
    [ObservableProperty] private string lastRatesUpdateText = "никогда";

    public SettingsViewModel(
        IDatabaseService db,
        ISettingsService settings,
        IBiometricService biometric,
        IExportService export,
        IExchangeRateService rates)
    {
        _db = db;
        _settings = settings;
        _biometric = biometric;
        _export = export;
        _rates = rates;
        Title = "Настройки";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var s = await _settings.GetAsync();
        BaseCurrency = s.BaseCurrency;
        DisplayCurrencies = s.DisplayCurrenciesCsv;
        BiometricEnabled = s.BiometricEnabled;
        LastRatesUpdateText = s.LastRatesUpdate?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "никогда";
    }

    [RelayCommand]
    private async Task SaveCurrencyAsync()
    {
        await _settings.SetBaseCurrencyAsync(BaseCurrency);
        await _settings.SetDisplayCurrenciesAsync(
            DisplayCurrencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (Application.Current?.MainPage is not null)
            await Application.Current.MainPage.DisplayAlert("Настройки", "Сохранено.", "OK");
    }

    [RelayCommand]
    private async Task ToggleBiometricAsync()
    {
        if (BiometricEnabled)
        {
            var available = await _biometric.IsAvailableAsync();
            if (!available)
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
        var ok = await _rates.RefreshRatesAsync(BaseCurrency);
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

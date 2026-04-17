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
        try
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

            try { BiometricAvailable = await _biometric.IsAvailableAsync(); } catch { BiometricAvailable = false; }
            BiometricEnabled = s.BiometricEnabled && BiometricAvailable;
            LastRatesUpdateText = s.LastRatesUpdate?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "никогда";

            try { await _identity.InitializeAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Settings.Load.Identity] {ex}"); }
            DeviceDisplayName = string.IsNullOrWhiteSpace(_identity.DisplayName) ? "Моё устройство" : _identity.DisplayName;
            var id = _identity.DeviceId ?? string.Empty;
            DeviceIdShort = id.Length > 12 ? id[..12] + "…" : id;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings.Load] {ex}");
            await UiAlerts.ShowAsync("Настройки", $"Не удалось загрузить: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveDeviceNameAsync()
    {
        try { await _identity.SetDisplayNameAsync(DeviceDisplayName); } catch { }
        await UiAlerts.ShowAsync("Устройство", "Имя сохранено.");
    }

    [RelayCommand]
    private async Task SaveCurrencyAsync()
    {
        try
        {
            var baseCode = SelectedBaseCurrency?.Code ?? "PLN";
            var selected = DisplayChoices.Where(c => c.IsSelected).Select(c => c.Currency.Code).ToList();
            if (selected.Count == 0) selected.Add(baseCode);
            if (!selected.Contains(baseCode)) selected.Insert(0, baseCode);

            await _settings.SetBaseCurrencyAsync(baseCode);
            await _settings.SetDisplayCurrenciesAsync(selected);
            await UiAlerts.ShowAsync("Настройки", "Сохранено.");
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
    }

    [RelayCommand]
    private async Task ToggleBiometricAsync()
    {
        try
        {
            if (BiometricEnabled)
            {
                if (!BiometricAvailable)
                {
                    BiometricEnabled = false;
                    await UiAlerts.ShowAsync("Биометрия", "Не настроена на устройстве.");
                    return;
                }
                bool ok;
                try { ok = await _biometric.AuthenticateAsync("Подтвердите включение биометрии"); }
                catch { ok = false; }
                if (!ok)
                {
                    BiometricEnabled = false;
                    return;
                }
            }
            await _settings.SetBiometricEnabledAsync(BiometricEnabled);
        }
        catch { }
    }

    [RelayCommand]
    private async Task DeletePeriodAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var confirm = await UiAlerts.ConfirmAsync(
                "Удалить операции",
                $"Удалить все операции с {DeleteFromDate:d} по {DeleteToDate:d}? Действие необратимо.",
                "Удалить", "Отмена");
            if (!confirm) return;

            var count = await _db.DeleteTransactionsInRangeAsync(
                DeleteFromDate.ToUniversalTime(),
                DeleteToDate.AddDays(1).AddSeconds(-1).ToUniversalTime());

            await UiAlerts.ShowAsync("Готово", $"Удалено: {count}");
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var path = await _export.ExportTransactionsCsvAsync();
            await _export.ShareExportAsync(path);
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RefreshRatesAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var baseCode = SelectedBaseCurrency?.Code ?? "PLN";
            var ok = await _rates.RefreshRatesAsync(baseCode);
            await LoadAsync();
            await UiAlerts.ShowAsync("Курсы", ok ? "Обновлено." : "Не удалось обновить.");
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task OpenAccountsAsync() => SafeNavigate(nameof(AccountsPage));

    [RelayCommand]
    private Task OpenCategoriesAsync() => SafeNavigate(nameof(CategoriesPage));

    [RelayCommand]
    private Task OpenFamiliesAsync() => SafeNavigate(nameof(FamilyListPage));

    [RelayCommand]
    private async Task ResetAllAsync()
    {
        try
        {
            var once = await UiAlerts.ConfirmAsync(
                "Сброс", "Удалить все данные приложения? Действие необратимо.", "Продолжить", "Отмена");
            if (!once) return;
            var twice = await UiAlerts.ConfirmAsync(
                "Подтверждение", "Вы уверены? Это удалит все операции, счета и настройки.", "Удалить всё", "Отмена");
            if (!twice) return;

            await _db.ResetAllDataAsync();
            await LoadAsync();
            await UiAlerts.ShowAsync("Готово", "Данные сброшены.");
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
    }

    private static async Task SafeNavigate(string route)
    {
        try
        {
            if (Shell.Current is null)
            {
                System.Diagnostics.Debug.WriteLine($"[SafeNavigate] Shell.Current is null for {route}");
                await UiAlerts.ShowAsync("Навигация", $"Не удалось открыть '{route}'.");
                return;
            }
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SafeNavigate] {route}: {ex}");
            await UiAlerts.ShowAsync("Ошибка навигации", ex.Message);
        }
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

public partial class OnboardingViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly ISettingsService _settings;
    private readonly IBiometricService _biometric;
    private readonly IExchangeRateService _rates;

    [ObservableProperty] private ObservableCollection<CurrencyInfo> availableCurrencies = new();
    [ObservableProperty] private ObservableCollection<CurrencyChoice> displayChoices = new();
    [ObservableProperty] private CurrencyInfo? selectedBaseCurrency;
    [ObservableProperty] private bool biometricEnabled;
    [ObservableProperty] private bool biometricAvailable;
    [ObservableProperty] private string statusText = string.Empty;

    public OnboardingViewModel(
        IDatabaseService db,
        ISettingsService settings,
        IBiometricService biometric,
        IExchangeRateService rates)
    {
        _db = db;
        _settings = settings;
        _biometric = biometric;
        _rates = rates;
        Title = "Добро пожаловать";
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

            var selectedCodes = s.DisplayCurrenciesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToUpperInvariant())
                .ToHashSet();

            DisplayChoices.Clear();
            foreach (var c in SupportedCurrencies.All)
            {
                DisplayChoices.Add(new CurrencyChoice
                {
                    Currency = c,
                    IsSelected = selectedCodes.Contains(c.Code)
                });
            }

            try { BiometricAvailable = await _biometric.IsAvailableAsync(); } catch { BiometricAvailable = false; }
            BiometricEnabled = s.BiometricEnabled && BiometricAvailable;
        }
        catch { /* never crash onboarding */ }
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var baseCode = SelectedBaseCurrency?.Code ?? "PLN";
            var selected = DisplayChoices.Where(c => c.IsSelected).Select(c => c.Currency.Code).ToList();
            if (selected.Count == 0) selected.Add(baseCode);
            if (!selected.Contains(baseCode)) selected.Insert(0, baseCode);

            var s = await _settings.GetAsync();
            s.BaseCurrency = baseCode;
            s.DisplayCurrenciesCsv = string.Join(",", selected);

            if (BiometricEnabled && BiometricAvailable)
            {
                bool ok;
                try { ok = await _biometric.AuthenticateAsync("Подтвердите биометрию для LocalWallet"); }
                catch { ok = false; }
                s.BiometricEnabled = ok;
                BiometricEnabled = ok;
            }
            else
            {
                s.BiometricEnabled = false;
            }

            s.IsOnboarded = true;
            await _db.SaveSettingsAsync(s);

            StatusText = "Загружаем курсы валют…";
            bool ratesOk;
            try { ratesOk = await _rates.RefreshRatesAsync(baseCode); }
            catch { ratesOk = false; }
            StatusText = ratesOk ? "Готово" : "Курсы не загрузились — обновим позже";

            UiAlerts.TrySetRootPage(new AppShell());
        }
        catch
        {
            StatusText = "Ошибка. Попробуйте ещё раз.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class CurrencyChoice : ObservableObject
{
    [ObservableProperty] private bool isSelected;
    public CurrencyInfo Currency { get; set; } = null!;
}

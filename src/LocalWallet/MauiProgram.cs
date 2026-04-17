using LocalWallet.Services;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.Services.Families;
using LocalWallet.Services.Sync;
using LocalWallet.ViewModels;
using LocalWallet.Views;
using Microcharts.Maui;
using Microsoft.Extensions.Logging;

namespace LocalWallet;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMicrocharts()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("MaterialIcons-Regular.ttf", "MatIcon");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Core services
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IBiometricService, BiometricService>();
        builder.Services.AddSingleton<IExportService, ExportService>();
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<NbpRatesProvider>();
        builder.Services.AddSingleton<FrankfurterRatesProvider>();
        builder.Services.AddSingleton<IExchangeRateService, ExchangeRateService>();

        // Crypto / identity
        builder.Services.AddSingleton<IDeviceIdentityService, DeviceIdentityService>();
        builder.Services.AddSingleton<IFamilyCryptoService, FamilyCryptoService>();
        builder.Services.AddSingleton<IPairingService, PairingService>();

        // Family + sync
        builder.Services.AddSingleton<IFamilyService, FamilyService>();
        builder.Services.AddSingleton<IProjector, Projector>();
        builder.Services.AddSingleton<IEventStore, EventStore>();
        builder.Services.AddSingleton<ISyncService, SyncService>();
        builder.Services.AddSingleton<IEntityWriter, EntityWriter>();

        // ViewModels
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<MultiCurrencyViewModel>();
        builder.Services.AddTransient<TransactionsViewModel>();
        builder.Services.AddTransient<AddTransactionViewModel>();
        builder.Services.AddTransient<AccountsViewModel>();
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<StatisticsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<FamilyListViewModel>();
        builder.Services.AddTransient<FamilyDetailsViewModel>();
        builder.Services.AddTransient<InviteViewModel>();
        builder.Services.AddTransient<JoinFamilyViewModel>();

        // Pages
        builder.Services.AddTransient<LockPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<MultiCurrencyPage>();
        builder.Services.AddTransient<TransactionsPage>();
        builder.Services.AddTransient<AddTransactionPage>();
        builder.Services.AddTransient<AccountsPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<StatisticsPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<FamilyListPage>();
        builder.Services.AddTransient<FamilyDetailsPage>();
        builder.Services.AddTransient<InvitePage>();
        builder.Services.AddTransient<JoinFamilyPage>();

        return builder.Build();
    }
}

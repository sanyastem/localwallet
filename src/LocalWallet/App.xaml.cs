using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.Views;

namespace LocalWallet;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = ResolveInitialPage();
        KickOffBackgroundWarmup();
        return new Window(page);
    }

    private Page ResolveInitialPage()
    {
        try
        {
            var db = _services.GetRequiredService<IDatabaseService>();
            var settings = db.GetSettingsAsync().GetAwaiter().GetResult();

            if (!settings.IsOnboarded)
                return _services.GetRequiredService<OnboardingPage>();

            if (settings.BiometricEnabled)
                return _services.GetRequiredService<LockPage>();
        }
        catch
        {
            // fall through to AppShell on any init error
        }

        return new AppShell();
    }

    private void KickOffBackgroundWarmup()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var db = _services.GetRequiredService<IDatabaseService>();
                var rates = _services.GetRequiredService<IExchangeRateService>();
                var s = await db.GetSettingsAsync();
                var stale = !s.LastRatesUpdate.HasValue ||
                            (DateTime.UtcNow - s.LastRatesUpdate.Value) > TimeSpan.FromHours(12);
                if (stale) await rates.RefreshRatesAsync(s.BaseCurrency);
            }
            catch { /* best-effort */ }
        });
    }
}

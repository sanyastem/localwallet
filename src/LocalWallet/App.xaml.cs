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
        var splash = BuildSplash();
        var window = new Window(splash);

        _ = Task.Run(() => InitializeAsync(window));

        return window;
    }

    private async Task InitializeAsync(Window window)
    {
        try
        {
            var db = _services.GetRequiredService<IDatabaseService>();
            var settings = await db.GetSettingsAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Page target;
                if (!settings.IsOnboarded)
                    target = _services.GetRequiredService<OnboardingPage>();
                else if (settings.BiometricEnabled)
                    target = _services.GetRequiredService<LockPage>();
                else
                    target = new AppShell();
                window.Page = target;
            });

            var rates = _services.GetRequiredService<IExchangeRateService>();
            var stale = !settings.LastRatesUpdate.HasValue ||
                        (DateTime.UtcNow - settings.LastRatesUpdate.Value) > TimeSpan.FromHours(12);
            if (stale)
            {
                try { await rates.RefreshRatesAsync(settings.BaseCurrency); }
                catch { /* best-effort */ }
            }
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                window.Page = new AppShell();
            });
        }
    }

    private static Page BuildSplash() =>
        new ContentPage
        {
            BackgroundColor = Color.FromArgb("#2E7D32"),
            Content = new Grid
            {
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 16,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Label
                            {
                                Text = "LocalWallet",
                                FontSize = 28,
                                TextColor = Colors.White,
                                HorizontalOptions = LayoutOptions.Center,
                                FontAttributes = FontAttributes.Bold
                            },
                            new ActivityIndicator
                            {
                                IsRunning = true,
                                Color = Colors.White,
                                HorizontalOptions = LayoutOptions.Center
                            }
                        }
                    }
                }
            }
        };
}

using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.Services.Sync;
using LocalWallet.Views;

namespace LocalWallet;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        InstallGlobalExceptionHandlers();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(BuildSplash());

        _ = Task.Run(async () =>
        {
            try { await InitializeAsync(window); }
            catch { /* last-resort */ }
        });

        return window;
    }

    private async Task InitializeAsync(Window window)
    {
        bool haveSettings = false;
        bool isOnboarded = false;
        bool biometricEnabled = false;
        DateTime? lastRatesUpdate = null;
        string baseCurrency = "PLN";

        try
        {
            var db = _services.GetRequiredService<IDatabaseService>();
            var appSettings = await db.GetSettingsAsync();
            isOnboarded = appSettings.IsOnboarded;
            biometricEnabled = appSettings.BiometricEnabled;
            lastRatesUpdate = appSettings.LastRatesUpdate;
            baseCurrency = string.IsNullOrWhiteSpace(appSettings.BaseCurrency) ? "PLN" : appSettings.BaseCurrency;
            haveSettings = true;

            try { await _services.GetRequiredService<IDeviceIdentityService>().InitializeAsync(); }
            catch { /* device identity is best-effort; P2P features will gracefully fail later */ }
        }
        catch
        {
            // DB init failure: send user to Shell anyway so they see something.
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                Page target;
                if (!haveSettings) target = new AppShell();
                else if (!isOnboarded) target = _services.GetRequiredService<OnboardingPage>();
                else if (biometricEnabled) target = _services.GetRequiredService<LockPage>();
                else target = new AppShell();
                window.Page = target;
            }
            catch
            {
                try { window.Page = new AppShell(); } catch { }
            }
        });

        if (haveSettings)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var rates = _services.GetRequiredService<IExchangeRateService>();
                    var stale = !lastRatesUpdate.HasValue ||
                                (DateTime.UtcNow - lastRatesUpdate.Value) > TimeSpan.FromHours(12);
                    if (stale) await rates.RefreshRatesAsync(baseCurrency);
                }
                catch { /* best-effort */ }
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    var sync = _services.GetRequiredService<ISyncService>();
                    await sync.StartListenerAsync();
                }
                catch { /* P2P boot is best-effort */ }
            });
        }
    }

    private static void InstallGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try { System.Diagnostics.Debug.WriteLine($"[LocalWallet.Unhandled] {e.ExceptionObject}"); } catch { }
            if (e.ExceptionObject is Exception ex) SurfaceErrorToUser(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try { System.Diagnostics.Debug.WriteLine($"[LocalWallet.UnobservedTask] {e.Exception}"); } catch { }
            SurfaceErrorToUser(e.Exception);
            try { e.SetObserved(); } catch { }
        };
#if ANDROID
        try
        {
            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
            {
                // v2.3.0 used to swallow these with e.Handled = true, leaving the UI
                // in a broken state (black screens, dead buttons) without any feedback.
                // Now we surface the error to the user AND keep the app alive.
                try { System.Diagnostics.Debug.WriteLine($"[LocalWallet.AndroidUnhandled] {e.Exception}"); } catch { }
                SurfaceErrorToUser(e.Exception);
                try { e.Handled = true; } catch { }
            };
        }
        catch { }
#endif
    }

    private static void SurfaceErrorToUser(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            var root = ex;
            while (root.InnerException is not null) root = root.InnerException;
            var message = ReferenceEquals(root, ex)
                ? ex.Message
                : $"{ex.GetType().Name}: {root.Message}";

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { await Services.UiAlerts.ShowAsync("Ошибка", message); } catch { }
            });
        }
        catch { }
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

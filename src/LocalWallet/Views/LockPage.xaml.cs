using LocalWallet.Services;

namespace LocalWallet.Views;

public partial class LockPage : ContentPage
{
    private readonly IBiometricService _biometric;

    public LockPage(IBiometricService biometric)
    {
        InitializeComponent();
        _biometric = biometric;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await TryUnlockAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LockPage.OnAppearing] {ex}"); }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        try { await TryUnlockAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LockPage.Retry] {ex}"); }
    }

    private async Task TryUnlockAsync()
    {
        bool ok;
        try { ok = await _biometric.AuthenticateAsync("Подтвердите вход в LocalWallet"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LockPage.Authenticate] {ex}");
            ok = false;
        }

        if (!ok) return;
        if (!UiAlerts.TrySetRootPage(new AppShell()))
        {
            await UiAlerts.ShowAsync("Вход", "Не удалось открыть главный экран.");
        }
    }
}

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
        try { await TryUnlockAsync(); } catch { }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        try { await TryUnlockAsync(); } catch { }
    }

    private async Task TryUnlockAsync()
    {
        bool ok;
        try { ok = await _biometric.AuthenticateAsync("Подтвердите вход в LocalWallet"); }
        catch { ok = false; }

        if (ok) UiAlerts.TrySetRootPage(new AppShell());
    }
}

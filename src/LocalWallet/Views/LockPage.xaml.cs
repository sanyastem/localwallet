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
        await TryUnlockAsync();
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await TryUnlockAsync();
    }

    private async Task TryUnlockAsync()
    {
        var ok = await _biometric.AuthenticateAsync("Подтвердите вход в LocalWallet");
        if (ok && Application.Current is not null)
        {
            Application.Current.Windows[0].Page = new AppShell();
        }
    }
}

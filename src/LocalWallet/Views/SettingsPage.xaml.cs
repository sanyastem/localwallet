using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;
    private bool _suppressBiometricToggle;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _suppressBiometricToggle = true;
        try { await _vm.LoadAsync(); }
        catch { }
        finally { _suppressBiometricToggle = false; }
    }

    private void OnBiometricToggled(object? sender, ToggledEventArgs e)
    {
        if (_suppressBiometricToggle) return;
        try
        {
            if (_vm.ToggleBiometricCommand.CanExecute(null))
                _vm.ToggleBiometricCommand.Execute(null);
        }
        catch { }
    }
}

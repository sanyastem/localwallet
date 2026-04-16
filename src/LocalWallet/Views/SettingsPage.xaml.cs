using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private void OnBiometricToggled(object? sender, ToggledEventArgs e)
    {
        if (_vm.ToggleBiometricCommand.CanExecute(null))
            _vm.ToggleBiometricCommand.Execute(null);
    }
}

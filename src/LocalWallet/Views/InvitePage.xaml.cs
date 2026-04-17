using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class InvitePage : ContentPage
{
    private readonly InviteViewModel _vm;

    public InvitePage(InviteViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.StartAsync(); } catch { }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { if (_vm.CancelCommand.CanExecute(null)) _vm.CancelCommand.Execute(null); } catch { }
    }
}

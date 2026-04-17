using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsViewModel _vm;

    public AccountsPage(AccountsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); } catch { }
    }
}

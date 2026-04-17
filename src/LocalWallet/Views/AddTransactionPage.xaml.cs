using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class AddTransactionPage : ContentPage
{
    private readonly AddTransactionViewModel _vm;

    public AddTransactionPage(AddTransactionViewModel vm)
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

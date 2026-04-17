using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class MultiCurrencyPage : ContentPage
{
    private readonly MultiCurrencyViewModel _vm;

    public MultiCurrencyPage(MultiCurrencyViewModel vm)
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

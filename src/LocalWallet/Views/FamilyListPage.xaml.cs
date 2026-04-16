using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class FamilyListPage : ContentPage
{
    private readonly FamilyListViewModel _vm;

    public FamilyListPage(FamilyListViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}

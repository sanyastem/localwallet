using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class FamilyDetailsPage : ContentPage
{
    private readonly FamilyDetailsViewModel _vm;

    public FamilyDetailsPage(FamilyDetailsViewModel vm)
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

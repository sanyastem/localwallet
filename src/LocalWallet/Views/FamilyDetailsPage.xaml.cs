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
        _vm.OnAttached();
        try { await _vm.LoadAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FamilyDetailsPage.OnAppearing] {ex}"); }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.OnDetached();
    }
}

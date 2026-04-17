using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class OnboardingPage : ContentPage
{
    private readonly OnboardingViewModel _vm;

    public OnboardingPage(OnboardingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadCommand.ExecuteAsync(null); } catch { }
    }
}

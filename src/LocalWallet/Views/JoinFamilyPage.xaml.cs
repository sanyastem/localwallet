using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class JoinFamilyPage : ContentPage
{
    private readonly JoinFamilyViewModel _vm;

    public JoinFamilyPage(JoinFamilyViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }
}

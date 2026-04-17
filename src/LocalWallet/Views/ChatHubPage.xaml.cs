using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class ChatHubPage : ContentPage
{
    private readonly ChatHubViewModel _vm;

    public ChatHubPage(ChatHubViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ChatHubPage.OnAppearing] {ex}"); }
    }
}

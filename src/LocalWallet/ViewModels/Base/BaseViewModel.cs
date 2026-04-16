using CommunityToolkit.Mvvm.ComponentModel;

namespace LocalWallet.ViewModels.Base;

public partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? title;
}

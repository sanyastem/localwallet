using LocalWallet.ViewModels;
using ZXing.Net.Maui;

namespace LocalWallet.Views;

public partial class JoinFamilyPage : ContentPage
{
    private readonly JoinFamilyViewModel _vm;

    public JoinFamilyPage(JoinFamilyViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        Scanner.Options = new BarcodeReaderOptions
        {
            AutoRotate = true,
            Formats = BarcodeFormat.QrCode,
            Multiple = false,
            TryHarder = true
        };
    }

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (e.Results is null || e.Results.Length == 0) return;
        var first = e.Results[0];
        var value = first.Value;
        if (string.IsNullOrEmpty(value)) return;
        await MainThread.InvokeOnMainThreadAsync(async () => await _vm.HandleScanAsync(value));
    }
}

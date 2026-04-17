using LocalWallet.ViewModels;
using ZXing.Net.Maui;

namespace LocalWallet.Views;

public partial class JoinFamilyPage : ContentPage
{
    private readonly JoinFamilyViewModel _vm;
    private bool _scanHandled;

    public JoinFamilyPage(JoinFamilyViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _scanHandled = false;
        _vm.IsScanning = false;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>();

            if (status == PermissionStatus.Granted)
            {
                _vm.ScanHint = "Наведите камеру на QR-код";
                _vm.IsScanning = true;
            }
            else
            {
                _vm.ScanHint = "Камера недоступна — вставьте JSON ниже";
                _vm.IsScanning = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JoinFamilyPage.Permissions] {ex}");
            _vm.ScanHint = "Камера недоступна — вставьте JSON ниже";
            _vm.IsScanning = false;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.IsScanning = false;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_scanHandled) return;
        var hit = e.Results?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r?.Value));
        if (hit is null) return;

        _scanHandled = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _vm.IsScanning = false;
            try { await _vm.HandleScannedTextAsync(hit.Value); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JoinFamilyPage.OnBarcodes] {ex}");
            }
        });
    }
}

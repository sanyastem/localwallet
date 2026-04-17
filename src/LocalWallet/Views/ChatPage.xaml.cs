using System.Collections.Specialized;
using LocalWallet.ViewModels;

namespace LocalWallet.Views;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _vm;

    public ChatPage(ChatViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        _vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.OnAttached();
        try { await _vm.LoadAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ChatPage.OnAppearing] {ex}"); }
        ScrollToEnd();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.OnDetached();
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add) ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        if (_vm.Messages.Count == 0) return;
        try
        {
            MessageList.ScrollTo(_vm.Messages.Count - 1, position: ScrollToPosition.End, animate: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatPage.ScrollToEnd] {ex}");
        }
    }
}

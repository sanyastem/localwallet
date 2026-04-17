using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services;
using LocalWallet.Services.Families;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

public partial class ChatHubViewModel : BaseViewModel
{
    private readonly IFamilyService _families;

    [ObservableProperty] private ObservableCollection<Models.Family> items = new();
    [ObservableProperty] private bool hasFamilies;
    [ObservableProperty] private bool hasMultiple;

    public ChatHubViewModel(IFamilyService families)
    {
        _families = families;
        Title = "Чат";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            var list = await _families.ListAsync();
            Items.Clear();
            foreach (var f in list) Items.Add(f);
            HasFamilies = Items.Count > 0;
            HasMultiple = Items.Count > 1;

            // Single-family quick-path: auto-navigate into the chat.
            if (Items.Count == 1) await OpenChatAsync(Items[0]);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatHub.Load] {ex}");
        }
    }

    [RelayCommand]
    private async Task CreateFamilyAsync()
    {
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync(nameof(FamilyListPage)); }
        catch (Exception ex) { await UiAlerts.ShowAsync("Навигация", ex.Message); }
    }

    [RelayCommand]
    private async Task JoinFamilyAsync()
    {
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync(nameof(JoinFamilyPage)); }
        catch (Exception ex) { await UiAlerts.ShowAsync("Навигация", ex.Message); }
    }

    [RelayCommand]
    private async Task OpenChatAsync(Models.Family family)
    {
        if (family is null) return;
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync($"{nameof(ChatPage)}?id={family.Id}"); }
        catch (Exception ex) { await UiAlerts.ShowAsync("Навигация", ex.Message); }
    }
}

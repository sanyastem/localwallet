using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services;
using LocalWallet.Services.Families;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

public partial class FamilyListViewModel : BaseViewModel
{
    private readonly IFamilyService _families;

    [ObservableProperty] private ObservableCollection<Models.Family> items = new();
    [ObservableProperty] private string newFamilyName = "Семья";

    public FamilyListViewModel(IFamilyService families)
    {
        _families = families;
        Title = "Семьи";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            var list = await _families.ListAsync();
            Items.Clear();
            foreach (var f in list) Items.Add(f);
        }
        catch { }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(NewFamilyName)) return;
        IsBusy = true;
        try
        {
            var f = await _families.CreateAsync(NewFamilyName);
            await LoadAsync();
            try { if (Shell.Current is not null) await Shell.Current.GoToAsync($"{nameof(FamilyDetailsPage)}?id={f.Id}"); }
            catch { }
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task JoinAsync()
    {
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync(nameof(JoinFamilyPage)); }
        catch { }
    }

    [RelayCommand]
    private async Task OpenAsync(Models.Family family)
    {
        if (family is null) return;
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync($"{nameof(FamilyDetailsPage)}?id={family.Id}"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FamilyList.Open] {ex}");
            await UiAlerts.ShowAsync("Ошибка навигации", ex.Message);
        }
    }

    [RelayCommand]
    private async Task OpenChatAsync(Models.Family family)
    {
        if (family is null) return;
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync($"{nameof(ChatPage)}?id={family.Id}"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FamilyList.OpenChat] {ex}");
            await UiAlerts.ShowAsync("Ошибка навигации", ex.Message);
        }
    }
}

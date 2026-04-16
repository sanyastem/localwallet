using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        var list = await _families.ListAsync();
        Items.Clear();
        foreach (var f in list) Items.Add(f);
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
            await Shell.Current.GoToAsync($"{nameof(FamilyDetailsPage)}?id={f.Id}");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task JoinAsync()
    {
        await Shell.Current.GoToAsync(nameof(JoinFamilyPage));
    }

    [RelayCommand]
    private async Task OpenAsync(Models.Family family)
    {
        if (family is null) return;
        await Shell.Current.GoToAsync($"{nameof(FamilyDetailsPage)}?id={family.Id}");
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services.Database;
using LocalWallet.Services.Family;
using LocalWallet.Services.Sync;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

[QueryProperty(nameof(FamilyIdString), "id")]
public partial class FamilyDetailsViewModel : BaseViewModel
{
    private readonly IFamilyService _families;
    private readonly IDatabaseService _db;
    private readonly ISyncService _sync;

    [ObservableProperty] private string familyIdString = string.Empty;
    [ObservableProperty] private Models.Family? family;
    [ObservableProperty] private ObservableCollection<Models.FamilyMember> members = new();
    [ObservableProperty] private string status = string.Empty;
    [ObservableProperty] private string peerHost = string.Empty;
    [ObservableProperty] private int peerPort = 47321;

    public FamilyDetailsViewModel(IFamilyService families, IDatabaseService db, ISyncService sync)
    {
        _families = families;
        _db = db;
        _sync = sync;
        Title = "Семья";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (!Guid.TryParse(FamilyIdString, out var id)) return;
        Family = await _db.GetFamilyAsync(id);
        if (Family is null) return;
        Title = Family.Name;
        var list = await _families.GetMembersAsync(id);
        Members.Clear();
        foreach (var m in list) Members.Add(m);
    }

    [RelayCommand]
    private async Task InviteAsync()
    {
        if (Family is null) return;
        await Shell.Current.GoToAsync($"{nameof(InvitePage)}?id={Family.Id}");
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        if (Family is null) return;
        if (string.IsNullOrWhiteSpace(PeerHost))
        {
            Status = "Введите IP устройства-участника";
            return;
        }
        IsBusy = true;
        try
        {
            Status = $"Синхронизация с {PeerHost}:{PeerPort}…";
            var result = await _sync.SyncWithPeerAsync(Family.Id, PeerHost, PeerPort);
            Status = result.Success
                ? $"Готово. Отправлено: {result.EventsSent}, получено: {result.EventsReceived}"
                : $"Ошибка: {result.Error}";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task StartListenerAsync()
    {
        var port = await _sync.StartListenerAsync();
        Status = $"Слушаю на порту {port}. Сообщите другому устройству: {LocalNetwork.GetLocalIPv4()}:{port}";
    }

    [RelayCommand]
    private void StopListener()
    {
        _sync.StopListener();
        Status = "Слушатель остановлен";
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        if (Family is null) return;
        if (Application.Current?.MainPage is null) return;
        var confirm = await Application.Current.MainPage.DisplayAlert(
            "Покинуть семью",
            $"Удалить локально семью «{Family.Name}»? Другие участники сохранят свои данные.",
            "Покинуть", "Отмена");
        if (!confirm) return;
        await _families.LeaveAsync(Family.Id);
        await Shell.Current.GoToAsync("..");
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services.Database;
using LocalWallet.Services.Families;
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
    [ObservableProperty] private string lastSyncedText = "никогда";

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
        LastSyncedText = Family.LastSyncedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "никогда";
    }

    [RelayCommand]
    private async Task SyncManualAsync()
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
            var r = await _sync.SyncWithPeerAsync(Family.Id, PeerHost, PeerPort);
            Status = r.Success
                ? $"Готово. Отправлено: {r.EventsSent}, получено: {r.EventsReceived}"
                : $"Ошибка: {r.Error}";
            await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task InviteAsync()
    {
        if (Family is null) return;
        await Shell.Current.GoToAsync($"{nameof(InvitePage)}?id={Family.Id}");
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        if (Family is null) return;
        if (Application.Current?.Windows.Count == 0 || Application.Current?.Windows[0]?.Page is null) return;
        var confirm = await Application.Current.Windows[0].Page!.DisplayAlertAsync(
            "Покинуть семью",
            $"Удалить локально семью «{Family.Name}»? Другие участники сохранят свои данные.",
            "Покинуть", "Отмена");
        if (!confirm) return;
        await _families.LeaveAsync(Family.Id);
        await Shell.Current.GoToAsync("..");
    }
}

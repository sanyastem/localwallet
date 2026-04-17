using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Services;
using LocalWallet.Services.Database;
using LocalWallet.Services.Families;
using LocalWallet.Services.Sync;
using LocalWallet.ViewModels.Base;
using LocalWallet.Views;

namespace LocalWallet.ViewModels;

[QueryProperty(nameof(FamilyIdString), "id")]
public partial class FamilyDetailsViewModel : BaseViewModel, IDisposable
{
    private readonly IFamilyService _families;
    private readonly IDatabaseService _db;
    private readonly ISyncService _sync;
    private readonly ILanDiscoveryService _discovery;

    [ObservableProperty] private string familyIdString = string.Empty;
    [ObservableProperty] private Models.Family? family;
    [ObservableProperty] private ObservableCollection<Models.FamilyMember> members = new();
    [ObservableProperty] private ObservableCollection<OnlinePeer> onlinePeers = new();
    [ObservableProperty] private string status = string.Empty;
    [ObservableProperty] private string peerHost = string.Empty;
    [ObservableProperty] private int peerPort = 47321;
    [ObservableProperty] private string lastSyncedText = "никогда";
    [ObservableProperty] private bool isOwner;

    public FamilyDetailsViewModel(IFamilyService families, IDatabaseService db, ISyncService sync, ILanDiscoveryService discovery)
    {
        _families = families;
        _db = db;
        _sync = sync;
        _discovery = discovery;
        Title = "Семья";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            if (!Guid.TryParse(FamilyIdString, out var id)) return;
            Family = await _db.GetFamilyAsync(id);
            if (Family is null) return;
            Title = Family.Name;
            IsOwner = Family.Role == "Owner";
            var list = await _families.GetMembersAsync(id);
            Members.Clear();
            foreach (var m in list) Members.Add(m);
            LastSyncedText = Family.LastSyncedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "никогда";
            RefreshPeers();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FamilyDetails.Load] {ex}"); }
    }

    public void OnAttached()
    {
        _discovery.PeerChanged += OnPeerChanged;
        RefreshPeers();
    }

    public void OnDetached() => _discovery.PeerChanged -= OnPeerChanged;

    private void OnPeerChanged(PeerEvent _)
    {
        MainThread.BeginInvokeOnMainThread(RefreshPeers);
    }

    private void RefreshPeers()
    {
        if (Family is null) return;
        var snapshot = _discovery.Snapshot(Family.Id);
        OnlinePeers.Clear();
        foreach (var p in snapshot)
        {
            OnlinePeers.Add(new OnlinePeer(
                p.DeviceId,
                string.IsNullOrWhiteSpace(p.DisplayName) ? "Участник" : p.DisplayName,
                p.Host,
                p.Port));
        }
    }

    [RelayCommand]
    private async Task OpenChatAsync()
    {
        if (Family is null) return;
        try
        {
            if (Shell.Current is null)
            {
                await UiAlerts.ShowAsync("Чат", "Не удалось открыть чат.");
                return;
            }
            await Shell.Current.GoToAsync($"{nameof(ChatPage)}?id={Family.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FamilyDetails.OpenChat] {ex}");
            await UiAlerts.ShowAsync("Ошибка навигации", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SyncPeerAsync(OnlinePeer? peer)
    {
        if (Family is null || peer is null || IsBusy) return;
        IsBusy = true;
        try
        {
            Status = $"Синхронизация с {peer.DisplayName}…";
            var r = await _sync.SyncWithPeerAsync(Family.Id, peer.Host, peer.Port);
            Status = r.Success
                ? $"Готово: отправлено {r.EventsSent}, получено {r.EventsReceived}"
                : $"Ошибка: {r.Error}";
            await LoadAsync();
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
        finally { IsBusy = false; }
    }

    public void Dispose() => OnDetached();

    [RelayCommand]
    private async Task RevokeMemberAsync(Models.FamilyMember? member)
    {
        if (Family is null || member is null || !IsOwner) return;
        var confirm = await UiAlerts.ConfirmAsync(
            "Удалить участника",
            $"Отозвать «{member.DisplayName}» из семьи? Его новые операции перестанут приниматься.",
            "Удалить", "Отмена");
        if (!confirm) return;
        try { await _families.RevokeMemberAsync(Family.Id, member.DeviceId); }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SyncManualAsync()
    {
        if (Family is null || IsBusy) return;
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
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task InviteAsync()
    {
        if (Family is null) return;
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync($"{nameof(InvitePage)}?id={Family.Id}"); }
        catch { }
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        if (Family is null) return;
        var confirm = await UiAlerts.ConfirmAsync(
            "Покинуть семью",
            $"Удалить локально семью «{Family.Name}»? Другие участники сохранят свои данные.",
            "Покинуть", "Отмена");
        if (!confirm) return;
        try { await _families.LeaveAsync(Family.Id); } catch { }
        try { if (Shell.Current is not null) await Shell.Current.GoToAsync(".."); } catch { }
    }
}

public record OnlinePeer(string DeviceId, string DisplayName, string Host, int Port)
{
    public string Endpoint => $"{Host}:{Port}";
}

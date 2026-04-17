using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;
using LocalWallet.Services.Families;
using LocalWallet.Services.Sync;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

[QueryProperty(nameof(FamilyIdText), "id")]
public partial class ChatViewModel : BaseViewModel, IDisposable
{
    private readonly IDatabaseService _db;
    private readonly IEntityWriter _writer;
    private readonly IDeviceIdentityService _identity;
    private readonly IFamilyService _families;
    private readonly IAutoSyncCoordinator _autoSync;
    private readonly ILanDiscoveryService _discovery;

    [ObservableProperty] private ObservableCollection<ChatBubble> messages = new();
    [ObservableProperty] private string draft = string.Empty;
    [ObservableProperty] private string familyName = string.Empty;
    [ObservableProperty] private int onlinePeersCount;
    [ObservableProperty] private string familyIdText = string.Empty;

    private Guid _familyId;
    private string _selfDeviceId = string.Empty;

    public ChatViewModel(
        IDatabaseService db,
        IEntityWriter writer,
        IDeviceIdentityService identity,
        IFamilyService families,
        IAutoSyncCoordinator autoSync,
        ILanDiscoveryService discovery)
    {
        _db = db;
        _writer = writer;
        _identity = identity;
        _families = families;
        _autoSync = autoSync;
        _discovery = discovery;
        Title = "Чат";
    }

    partial void OnFamilyIdTextChanged(string value)
    {
        if (Guid.TryParse(value, out var id)) _familyId = id;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_familyId == Guid.Empty) return;

        try
        {
            try { await _identity.InitializeAsync(); } catch { }
            _selfDeviceId = _identity.DeviceId ?? string.Empty;

            var family = await _db.GetFamilyAsync(_familyId);
            FamilyName = family?.Name ?? "Чат";

            var members = await _families.GetMembersAsync(_familyId);
            var nameByDevice = members
                .Where(m => m.RevokedAt is null)
                .ToDictionary(m => m.DeviceId, m => m.DisplayName, StringComparer.Ordinal);

            var raw = await _db.GetChatMessagesAsync(_familyId);
            Messages.Clear();
            foreach (var m in raw)
            {
                nameByDevice.TryGetValue(m.AuthorDeviceId, out var name);
                Messages.Add(ToBubble(m, name));
            }

            OnlinePeersCount = _discovery.Snapshot(_familyId).Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Chat.Load] {ex}");
            await UiAlerts.ShowAsync("Чат", $"Не удалось загрузить: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var text = (Draft ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text) || _familyId == Guid.Empty) return;

        Draft = string.Empty;

        try
        {
            if (string.IsNullOrEmpty(_selfDeviceId))
            {
                try { await _identity.InitializeAsync(); } catch { }
                _selfDeviceId = _identity.DeviceId ?? string.Empty;
            }

            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                FamilyId = _familyId,
                AuthorDeviceId = _selfDeviceId,
                AuthorDisplayName = _identity.DisplayName ?? string.Empty,
                Text = text,
                SentAt = DateTime.UtcNow
            };
            await _writer.SaveChatMessageAsync(msg);

            Messages.Add(ToBubble(msg, _identity.DisplayName));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Chat.Send] {ex}");
            Draft = text; // give the text back
            await UiAlerts.ShowAsync("Чат", $"Не удалось отправить: {ex.Message}");
        }
    }

    public void OnAttached()
    {
        _autoSync.FamilySynced += OnFamilySynced;
        _discovery.PeerChanged += OnPeerChanged;
    }

    public void OnDetached()
    {
        _autoSync.FamilySynced -= OnFamilySynced;
        _discovery.PeerChanged -= OnPeerChanged;
    }

    private void OnFamilySynced(Guid familyId)
    {
        if (familyId != _familyId) return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await LoadAsync(); } catch { }
        });
    }

    private void OnPeerChanged(PeerEvent e)
    {
        if (e.Peer.FamilyId != _familyId) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { OnlinePeersCount = _discovery.Snapshot(_familyId).Count; } catch { }
        });
    }

    private ChatBubble ToBubble(ChatMessage m, string? authorName)
    {
        var isMine = string.Equals(m.AuthorDeviceId, _selfDeviceId, StringComparison.Ordinal);
        var display = !string.IsNullOrWhiteSpace(authorName)
            ? authorName!
            : (!string.IsNullOrWhiteSpace(m.AuthorDisplayName) ? m.AuthorDisplayName : "Участник");
        return new ChatBubble
        {
            Id = m.Id,
            Text = m.Text,
            SentLocal = m.SentAt.ToLocalTime(),
            AuthorName = isMine ? "Я" : display,
            Initials = GetInitials(isMine ? (_identity.DisplayName ?? "Я") : display),
            IsMine = isMine
        };
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var trimmed = name.Trim();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[1][0])}";
        return char.ToUpper(trimmed[0]).ToString();
    }

    public void Dispose() => OnDetached();
}

public class ChatBubble
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime SentLocal { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public bool IsMine { get; set; }

    public string TimeFormatted => SentLocal.ToString(SentLocal.Date == DateTime.Today ? "HH:mm" : "dd.MM HH:mm");
    public LayoutOptions BubbleAlignment => IsMine ? LayoutOptions.End : LayoutOptions.Start;
    // Liquid-glass tinted surfaces. Mine = accent-tinted glass; other = neutral glass.
    public Color BubbleBg => IsMine ? Color.FromArgb("#D9295F2E") : Color.FromArgb("#CCFFFFFF");
    public Color BubbleFg => IsMine ? Colors.White : Color.FromArgb("#111714");
    public Color BubbleStroke => IsMine ? Color.FromArgb("#33FFFFFF") : Color.FromArgb("#1A000000");
    public Color MetaFg => IsMine ? Color.FromArgb("#CCFFFFFF") : Color.FromArgb("#627267");
    public bool ShowAvatar => !IsMine;
    public Color AvatarBg => Color.FromArgb("#66BB6A");
}

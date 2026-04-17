using LocalWallet.Services.Families;

namespace LocalWallet.Services.Sync;

public interface IAutoSyncCoordinator
{
    void Start();
    void Stop();

    // Raised when a remote sync changes local state — ViewModels can refresh UI.
    event Action<Guid>? FamilySynced;
}

public class AutoSyncCoordinator : IAutoSyncCoordinator
{
    private static readonly TimeSpan PerPeerCooldown = TimeSpan.FromSeconds(3);

    private readonly ILanDiscoveryService _discovery;
    private readonly ISyncService _sync;
    private readonly IFamilyService _families;

    private readonly Dictionary<(Guid FamilyId, string DeviceId), DateTime> _lastSync = new();
    private readonly object _gate = new();
    private bool _running;

    public AutoSyncCoordinator(ILanDiscoveryService discovery, ISyncService sync, IFamilyService families)
    {
        _discovery = discovery;
        _sync = sync;
        _families = families;
    }

    public event Action<Guid>? FamilySynced;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _discovery.PeerChanged += OnPeerChanged;
        _sync.LocalEventAppended += OnLocalEventAppended;
        _sync.SyncCompleted += OnSyncCompleted;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _discovery.PeerChanged -= OnPeerChanged;
        _sync.LocalEventAppended -= OnLocalEventAppended;
        _sync.SyncCompleted -= OnSyncCompleted;
    }

    private void OnSyncCompleted(Guid familyId) => FamilySynced?.Invoke(familyId);

    private void OnPeerChanged(PeerEvent ev)
    {
        if (ev.Vanished) return;
        _ = SyncWithPeerDebouncedAsync(ev.Peer);
    }

    private void OnLocalEventAppended()
    {
        _ = PushToAllPeersAsync();
    }

    private async Task PushToAllPeersAsync()
    {
        try
        {
            var families = await _families.ListAsync();
            foreach (var family in families)
            {
                foreach (var peer in _discovery.Snapshot(family.Id))
                {
                    _ = SyncWithPeerDebouncedAsync(peer);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoSync.Push] {ex}");
        }
    }

    private async Task SyncWithPeerDebouncedAsync(DiscoveredPeer peer)
    {
        var key = (peer.FamilyId, peer.DeviceId);
        lock (_gate)
        {
            if (_lastSync.TryGetValue(key, out var last) && DateTime.UtcNow - last < PerPeerCooldown)
                return;
            _lastSync[key] = DateTime.UtcNow;
        }

        try
        {
            var result = await _sync.SyncWithPeerAsync(peer.FamilyId, peer.Host, peer.Port);
            if (result.Success && (result.EventsReceived > 0 || result.EventsSent > 0))
            {
                FamilySynced?.Invoke(peer.FamilyId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoSync.Peer {peer.Host}:{peer.Port}] {ex}");
        }
    }
}

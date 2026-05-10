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
    private static readonly TimeSpan LastSyncEntryTtl = TimeSpan.FromMinutes(10);

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
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (_lastSync.TryGetValue(key, out var last) && now - last < PerPeerCooldown)
                return;
            _lastSync[key] = now;
            // Cheap inline cleanup: drop entries older than the TTL so the map
            // doesn't grow unbounded as peers come and go on the LAN.
            if (_lastSync.Count > 64)
            {
                var stale = _lastSync.Where(kv => now - kv.Value > LastSyncEntryTtl).Select(kv => kv.Key).ToList();
                foreach (var k in stale) _lastSync.Remove(k);
            }
        }

        try
        {
            // SyncWithPeerAsync raises ISyncService.SyncCompleted on success,
            // which AutoSyncCoordinator forwards as FamilySynced — no need to
            // raise it again here (would cause every refreshing VM to reload twice).
            await _sync.SyncWithPeerAsync(peer.FamilyId, peer.Host, peer.Port);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AutoSync.Peer {peer.Host}:{peer.Port}] {ex}");
        }
    }
}

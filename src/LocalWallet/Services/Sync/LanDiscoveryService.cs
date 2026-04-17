using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;
using LocalWallet.Services.Families;

namespace LocalWallet.Services.Sync;

public record DiscoveredPeer(
    string DeviceId,
    string DisplayName,
    Guid FamilyId,
    string Host,
    int Port,
    DateTime LastSeenUtc)
{
    public bool IsStale(TimeSpan ttl) => DateTime.UtcNow - LastSeenUtc > ttl;
}

public record PeerEvent(DiscoveredPeer Peer, bool Vanished = false);

public interface ILanDiscoveryService
{
    event Action<PeerEvent>? PeerChanged;

    IReadOnlyList<DiscoveredPeer> Snapshot(Guid familyId);
    Task StartAsync(CancellationToken ct = default);
    void Stop();
}

public class LanDiscoveryService : ILanDiscoveryService
{
    // Separate from the TCP sync port. Fixed well-known UDP port for the beacon.
    public const int BeaconPort = 47322;
    private static readonly TimeSpan BeaconInterval = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PeerTtl = TimeSpan.FromSeconds(30);
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("LWALLET1");

    private readonly IDeviceIdentityService _identity;
    private readonly IFamilyService _families;
    private readonly ISyncService _sync;

    private readonly Dictionary<(Guid FamilyId, string DeviceId), DiscoveredPeer> _peers = new();
    private readonly object _peersLock = new();

    private CancellationTokenSource? _cts;
    private UdpClient? _listener;
    private Task? _listenerTask;
    private Task? _broadcasterTask;
    private Task? _prunerTask;

    public LanDiscoveryService(
        IDeviceIdentityService identity,
        IFamilyService families,
        ISyncService sync)
    {
        _identity = identity;
        _families = families;
        _sync = sync;
    }

    public event Action<PeerEvent>? PeerChanged;

    public IReadOnlyList<DiscoveredPeer> Snapshot(Guid familyId)
    {
        lock (_peersLock)
        {
            return _peers.Values
                .Where(p => p.FamilyId == familyId && !p.IsStale(PeerTtl))
                .ToList();
        }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_cts is not null) return;

        try { await _identity.InitializeAsync(); }
        catch { /* keep going — device id may just be the local fallback */ }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        try
        {
            _listener = new UdpClient(AddressFamily.InterNetwork)
            {
                EnableBroadcast = true
            };
            _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, BeaconPort));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LanDiscovery] listener bind failed: {ex}");
            _listener?.Dispose();
            _listener = null;
        }

        _listenerTask = _listener is null ? Task.CompletedTask : Task.Run(() => ListenLoopAsync(_listener, token));
        _broadcasterTask = Task.Run(() => BroadcastLoopAsync(token));
        _prunerTask = Task.Run(() => PruneLoopAsync(token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Dispose(); } catch { }
        _listener = null;
        _cts = null;
        lock (_peersLock) _peers.Clear();
    }

    private async Task ListenLoopAsync(UdpClient listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await listener.ReceiveAsync(ct);
                HandleFrame(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanDiscovery.Listen] {ex}");
                await Task.Delay(500, ct).ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await BroadcastOnceAsync(ct); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LanDiscovery.Broadcast] {ex}"); }

            try { await Task.Delay(BeaconInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PruneLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
            catch (OperationCanceledException) { break; }

            List<DiscoveredPeer> stale;
            lock (_peersLock)
            {
                stale = _peers.Values.Where(p => p.IsStale(PeerTtl)).ToList();
                foreach (var p in stale) _peers.Remove((p.FamilyId, p.DeviceId));
            }
            foreach (var p in stale) PeerChanged?.Invoke(new PeerEvent(p, Vanished: true));
        }
    }

    private async Task BroadcastOnceAsync(CancellationToken ct)
    {
        var port = _sync.ListenPort;
        if (port is null or <= 0) return;

        var families = await _families.ListAsync();
        if (families.Count == 0) return;

        var deviceId = _identity.DeviceId ?? string.Empty;
        var displayName = _identity.DisplayName ?? string.Empty;

        foreach (var family in families)
        {
            var payload = new BeaconPayload
            {
                DeviceId = deviceId,
                DisplayName = displayName,
                FamilyId = family.Id.ToString("D"),
                SyncPort = port.Value
            };
            var frame = BuildFrame(payload);

            foreach (var target in EnumerateBroadcastTargets())
            {
                try
                {
                    using var sender = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                    await sender.SendAsync(frame, frame.Length, new IPEndPoint(target, BeaconPort));
                }
                catch { /* interface-specific failures are fine */ }
            }
        }
    }

    private void HandleFrame(byte[] buffer, IPEndPoint sender)
    {
        if (buffer.Length < Magic.Length + 4) return;
        for (int i = 0; i < Magic.Length; i++) if (buffer[i] != Magic[i]) return;

        BeaconPayload? payload;
        try
        {
            var json = Encoding.UTF8.GetString(buffer, Magic.Length, buffer.Length - Magic.Length);
            payload = JsonSerializer.Deserialize<BeaconPayload>(json);
        }
        catch { return; }

        if (payload is null) return;
        if (string.IsNullOrEmpty(payload.DeviceId)) return;
        if (!Guid.TryParse(payload.FamilyId, out var familyId)) return;

        // ignore beacons from ourselves
        if (!string.IsNullOrEmpty(_identity.DeviceId) &&
            string.Equals(payload.DeviceId, _identity.DeviceId, StringComparison.Ordinal)) return;

        var peer = new DiscoveredPeer(
            payload.DeviceId,
            payload.DisplayName ?? string.Empty,
            familyId,
            sender.Address.ToString(),
            payload.SyncPort,
            DateTime.UtcNow);

        bool isNew;
        lock (_peersLock)
        {
            var key = (familyId, payload.DeviceId);
            isNew = !_peers.ContainsKey(key);
            _peers[key] = peer;
        }
        PeerChanged?.Invoke(new PeerEvent(peer, Vanished: false));
        if (isNew) System.Diagnostics.Debug.WriteLine($"[LanDiscovery] peer {peer.DisplayName}@{peer.Host}:{peer.Port} family={familyId:N}");
    }

    private static byte[] BuildFrame(BeaconPayload p)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(p);
        var frame = new byte[Magic.Length + body.Length];
        Buffer.BlockCopy(Magic, 0, frame, 0, Magic.Length);
        Buffer.BlockCopy(body, 0, frame, Magic.Length, body.Length);
        return frame;
    }

    private static IEnumerable<IPAddress> EnumerateBroadcastTargets()
    {
        yield return IPAddress.Broadcast;

        NetworkInterface[] nics;
        try { nics = NetworkInterface.GetAllNetworkInterfaces(); }
        catch { yield break; }

        foreach (var nic in nics)
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            IPInterfaceProperties props;
            try { props = nic.GetIPProperties(); }
            catch { continue; }

            foreach (var ua in props.UnicastAddresses)
            {
                if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var bcast = GetBroadcastAddress(ua.Address, ua.IPv4Mask);
                if (bcast is not null) yield return bcast;
            }
        }
    }

    private static IPAddress? GetBroadcastAddress(IPAddress address, IPAddress? mask)
    {
        if (mask is null) return null;
        var addrBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        if (addrBytes.Length != maskBytes.Length) return null;
        var bcast = new byte[addrBytes.Length];
        for (int i = 0; i < addrBytes.Length; i++)
            bcast[i] = (byte)(addrBytes[i] | (maskBytes[i] ^ 0xFF));
        return new IPAddress(bcast);
    }

    private class BeaconPayload
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FamilyId { get; set; } = string.Empty;
        public int SyncPort { get; set; }
    }
}

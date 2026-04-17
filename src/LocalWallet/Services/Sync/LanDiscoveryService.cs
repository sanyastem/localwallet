using System.Collections.ObjectModel;
using Zeroconf;

namespace LocalWallet.Services.Sync;

public class LanDiscoveryService : ILanDiscoveryService
{
    private const string ServiceName = "_localwallet._tcp.local.";

    private readonly ObservableCollection<DiscoveredPeer> _peers = new();
    public ReadOnlyObservableCollection<DiscoveredPeer> Peers { get; }

    private CancellationTokenSource? _cts;
    private Task? _advertiseTask;
    private Task? _browseTask;

    public LanDiscoveryService()
    {
        Peers = new ReadOnlyObservableCollection<DiscoveredPeer>(_peers);
    }

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public async Task StartAsync(string deviceIdShort, string displayName, int listenPort, CancellationToken ct = default)
    {
        await StopAsync();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        var service = new Service
        {
            Name = $"localwallet-{deviceIdShort}",
            ServiceName = ServiceName,
            Port = (ushort)listenPort,
            Ttl = 120
        };
        service.AddProperty("did", deviceIdShort);
        service.AddProperty("name", displayName);

        _advertiseTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ZeroconfResolver.RegisterServiceAsync(service, token);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(TimeSpan.FromSeconds(15), token).ContinueWith(_ => { }); }
            }
        }, token);

        _browseTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try { await RefreshAsync(token); }
                catch (OperationCanceledException) { break; }
                catch { /* ignore transient errors */ }
                try { await Task.Delay(TimeSpan.FromSeconds(30), token); }
                catch { break; }
            }
        }, token);
    }

    public async Task StopAsync()
    {
        try { _cts?.Cancel(); } catch { }
        _cts = null;
        try { if (_advertiseTask is not null) await _advertiseTask.ConfigureAwait(false); } catch { }
        try { if (_browseTask is not null) await _browseTask.ConfigureAwait(false); } catch { }
        _advertiseTask = null;
        _browseTask = null;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        IReadOnlyList<IZeroconfHost> hosts;
        try
        {
            hosts = await ZeroconfResolver.ResolveAsync(ServiceName, scanTime: TimeSpan.FromSeconds(3), cancellationToken: ct);
        }
        catch
        {
            return;
        }

        var seen = new HashSet<string>();
        foreach (var host in hosts)
        {
            if (host.Services.Count == 0) continue;
            foreach (var (_, svc) in host.Services)
            {
                var port = svc.Port;
                var ip = host.IPAddress;
                var did = svc.Properties.SelectMany(p => p).FirstOrDefault(kv => kv.Key == "did").Value ?? "?";
                var name = svc.Properties.SelectMany(p => p).FirstOrDefault(kv => kv.Key == "name").Value ?? host.DisplayName;
                var key = $"{did}|{ip}:{port}";
                seen.Add(key);
                var peer = new DiscoveredPeer(did, name, ip, port, DateTime.UtcNow);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var existing = _peers.FirstOrDefault(p => p.DeviceIdShort == did && p.Host == ip && p.Port == port);
                    if (existing is not null) _peers.Remove(existing);
                    _peers.Add(peer);
                });
            }
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                var p = _peers[i];
                var key = $"{p.DeviceIdShort}|{p.Host}:{p.Port}";
                var age = DateTime.UtcNow - p.SeenAt;
                if (!seen.Contains(key) && age > TimeSpan.FromMinutes(2)) _peers.RemoveAt(i);
            }
        });
    }
}

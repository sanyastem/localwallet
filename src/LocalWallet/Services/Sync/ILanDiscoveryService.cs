using System.Collections.ObjectModel;

namespace LocalWallet.Services.Sync;

public record DiscoveredPeer(string DeviceIdShort, string DisplayName, string Host, int Port, DateTime SeenAt);

public interface ILanDiscoveryService
{
    ReadOnlyObservableCollection<DiscoveredPeer> Peers { get; }

    Task StartAsync(string deviceIdShort, string displayName, int listenPort, CancellationToken ct = default);

    Task StopAsync();

    Task RefreshAsync(CancellationToken ct = default);

    bool IsRunning { get; }
}

namespace LocalWallet.Services.Sync;

public record SyncResult(bool Success, int EventsSent, int EventsReceived, string? Error = null);

public interface ISyncService
{
    Task<SyncResult> SyncWithPeerAsync(Guid familyId, string host, int port, CancellationToken ct = default);

    Task<int> StartListenerAsync(Guid? inviteFamilyId = null, CancellationToken ct = default);

    void StopListener();

    bool IsListening { get; }

    int? ListenPort { get; }
}

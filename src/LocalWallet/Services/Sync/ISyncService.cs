namespace LocalWallet.Services.Sync;

public record SyncResult(bool Success, int EventsSent, int EventsReceived, string? Error = null);

public interface ISyncService
{
    Task<SyncResult> SyncWithPeerAsync(Guid familyId, string host, int port, CancellationToken ct = default);

    Task<int> StartListenerAsync(Guid? inviteFamilyId = null, CancellationToken ct = default);

    void StopListener();

    bool IsListening { get; }

    int? ListenPort { get; }

    // Raised after a local event is appended to the store. Auto-sync can use it
    // to push the event to currently-known peers.
    event Action? LocalEventAppended;

    // Raised after either side of a sync exchange completes with any events
    // actually moving. ViewModels listen to refresh lists live.
    event Action<Guid>? SyncCompleted;

    void NotifyLocalEventAppended();
}

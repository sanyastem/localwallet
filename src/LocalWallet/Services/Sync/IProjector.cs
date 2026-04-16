using LocalWallet.Models;

namespace LocalWallet.Services.Sync;

public interface IProjector
{
    Task ProjectAsync(SyncEvent ev, byte[] plaintextPayload);
}

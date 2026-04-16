using LocalWallet.Models;

namespace LocalWallet.Services.Sync;

public record EventDraft(
    Guid FamilyId,
    EntityType EntityType,
    Guid EntityId,
    EventOperation Operation,
    byte[] PayloadPlaintext);

public interface IEventStore
{
    Task<SyncEvent> AppendLocalAsync(EventDraft draft, byte[] familyKey);

    Task<bool> AcceptRemoteAsync(SyncEvent ev, byte[] familyKey, byte[] authorPublicKey);

    Task<byte[]?> DecryptPayloadAsync(SyncEvent ev, byte[] familyKey);
}

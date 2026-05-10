using LocalWallet.Models;
using LocalWallet.Services.Crypto;
using LocalWallet.Services.Database;

namespace LocalWallet.Services.Sync;

public class EventStore : IEventStore
{
    private readonly IDatabaseService _db;
    private readonly IDeviceIdentityService _identity;
    private readonly IFamilyCryptoService _crypto;
    private readonly IProjector _projector;
    private readonly SemaphoreSlim _appendLock = new(1, 1);

    public EventStore(
        IDatabaseService db,
        IDeviceIdentityService identity,
        IFamilyCryptoService crypto,
        IProjector projector)
    {
        _db = db;
        _identity = identity;
        _crypto = crypto;
        _projector = projector;
    }

    public async Task<SyncEvent> AppendLocalAsync(EventDraft draft, byte[] familyKey)
    {
        await _appendLock.WaitAsync();
        try
        {
            var settings = await _db.GetSettingsAsync();
            var nextClock = settings.CurrentLamportClock + 1;

            var envelope = _crypto.Seal(familyKey, draft.PayloadPlaintext, AssociatedData(draft, nextClock));

            var ev = new SyncEvent
            {
                Id = Guid.NewGuid(),
                FamilyId = draft.FamilyId,
                EntityType = draft.EntityType,
                EntityId = draft.EntityId,
                Operation = draft.Operation,
                EncryptedPayload = envelope.CiphertextAndTag,
                Nonce = envelope.Nonce,
                LamportClock = nextClock,
                AuthorDeviceId = _identity.DeviceId,
                CreatedAt = DateTime.UtcNow,
                Signature = Array.Empty<byte>()
            };

            ev.Signature = _identity.Sign(GetSigningInput(ev));

            await _db.AppendEventAsync(ev);
            settings.CurrentLamportClock = nextClock;
            await _db.SaveSettingsAsync(settings);

            try
            {
                await _projector.ProjectAsync(ev, draft.PayloadPlaintext);
            }
            catch
            {
                // Keep log and projection consistent: if applying the event fails,
                // remove it from the log so a retry can re-emit it.
                try { await _db.DeleteEventAsync(ev.Id); } catch { }
                throw;
            }
            return ev;
        }
        finally
        {
            _appendLock.Release();
        }
    }

    public async Task<bool> AcceptRemoteAsync(SyncEvent ev, byte[] familyKey, byte[] authorPublicKey)
    {
        if (await _db.EventExistsAsync(ev.Id)) return false;

        if (!_identity.Verify(authorPublicKey, GetSigningInput(ev), ev.Signature)) return false;

        var author = await _db.FindFamilyMemberAsync(ev.FamilyId, ev.AuthorDeviceId);
        if (author?.RevokedAt is not null) return false;

        if (ev.EntityType == EntityType.Member && author?.Role != "Owner") return false;

        var plaintext = _crypto.Open(
            familyKey,
            new AeadEnvelope(ev.Nonce, ev.EncryptedPayload),
            AssociatedDataFromEvent(ev));
        if (plaintext is null) return false;

        await _appendLock.WaitAsync();
        try
        {
            var settings = await _db.GetSettingsAsync();
            if (ev.LamportClock > settings.CurrentLamportClock)
            {
                settings.CurrentLamportClock = ev.LamportClock;
                await _db.SaveSettingsAsync(settings);
            }

            await _db.AppendEventAsync(ev);
            try
            {
                await _projector.ProjectAsync(ev, plaintext);
            }
            catch
            {
                // Keep log and projection consistent: drop the event so a future
                // sync re-delivers it instead of EventExists short-circuiting.
                try { await _db.DeleteEventAsync(ev.Id); } catch { }
                throw;
            }
            return true;
        }
        finally
        {
            _appendLock.Release();
        }
    }

    public Task<byte[]?> DecryptPayloadAsync(SyncEvent ev, byte[] familyKey)
    {
        var plaintext = _crypto.Open(
            familyKey,
            new AeadEnvelope(ev.Nonce, ev.EncryptedPayload),
            AssociatedDataFromEvent(ev));
        return Task.FromResult(plaintext);
    }

    private static byte[] AssociatedData(EventDraft d, long clock)
    {
        return System.Text.Encoding.UTF8.GetBytes(
            $"{d.FamilyId:N}|{(int)d.EntityType}|{d.EntityId:N}|{(int)d.Operation}|{clock}");
    }

    private static byte[] AssociatedDataFromEvent(SyncEvent e)
    {
        return System.Text.Encoding.UTF8.GetBytes(
            $"{e.FamilyId:N}|{(int)e.EntityType}|{e.EntityId:N}|{(int)e.Operation}|{e.LamportClock}");
    }

    private static byte[] GetSigningInput(SyncEvent e)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(e.Id.ToByteArray());
        bw.Write(e.FamilyId.ToByteArray());
        bw.Write((int)e.EntityType);
        bw.Write(e.EntityId.ToByteArray());
        bw.Write((int)e.Operation);
        bw.Write(e.Nonce);
        bw.Write(e.EncryptedPayload);
        bw.Write(e.LamportClock);
        bw.Write(e.AuthorDeviceId);
        bw.Write(e.CreatedAt.ToBinary());
        return ms.ToArray();
    }
}

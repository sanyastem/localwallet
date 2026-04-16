using LocalWallet.Models;

namespace LocalWallet.Services.Sync;

public enum SyncMessageType
{
    Hello = 1,
    HelloAck = 2,
    VectorClock = 3,
    EventBatch = 4,
    Done = 5,
    Error = 99
}

public class SyncHello
{
    public string FromDeviceId { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
}

public class SyncHelloAck
{
    public string FromDeviceId { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = string.Empty;
    public string ChallengeSignatureBase64 { get; set; } = string.Empty;
    public string OurChallenge { get; set; } = string.Empty;
    public string FromPublicKeyBase64 { get; set; } = string.Empty;
}

public class VectorClockMessage
{
    public Dictionary<string, long> Clocks { get; set; } = new();
}

public class EventBatchMessage
{
    public List<SyncEventWire> Events { get; set; } = new();
}

public class SyncEventWire
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public int EntityType { get; set; }
    public Guid EntityId { get; set; }
    public int Operation { get; set; }
    public string EncryptedPayloadBase64 { get; set; } = string.Empty;
    public string NonceBase64 { get; set; } = string.Empty;
    public long LamportClock { get; set; }
    public string AuthorDeviceId { get; set; } = string.Empty;
    public string SignatureBase64 { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public static SyncEventWire From(SyncEvent e) => new()
    {
        Id = e.Id,
        FamilyId = e.FamilyId,
        EntityType = (int)e.EntityType,
        EntityId = e.EntityId,
        Operation = (int)e.Operation,
        EncryptedPayloadBase64 = Convert.ToBase64String(e.EncryptedPayload),
        NonceBase64 = Convert.ToBase64String(e.Nonce),
        LamportClock = e.LamportClock,
        AuthorDeviceId = e.AuthorDeviceId,
        SignatureBase64 = Convert.ToBase64String(e.Signature),
        CreatedAt = e.CreatedAt
    };

    public SyncEvent ToEvent() => new()
    {
        Id = Id,
        FamilyId = FamilyId,
        EntityType = (EntityType)EntityType,
        EntityId = EntityId,
        Operation = (EventOperation)Operation,
        EncryptedPayload = Convert.FromBase64String(EncryptedPayloadBase64),
        Nonce = Convert.FromBase64String(NonceBase64),
        LamportClock = LamportClock,
        AuthorDeviceId = AuthorDeviceId,
        Signature = Convert.FromBase64String(SignatureBase64),
        CreatedAt = CreatedAt
    };
}

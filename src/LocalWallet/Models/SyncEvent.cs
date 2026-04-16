using SQLite;

namespace LocalWallet.Models;

public enum EntityType
{
    Transaction = 0,
    Account = 1,
    Category = 2
}

public enum EventOperation
{
    Upsert = 0,
    Delete = 1
}

[Table("SyncEvents")]
public class SyncEvent
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, NotNull]
    public Guid FamilyId { get; set; }

    public EntityType EntityType { get; set; }

    [Indexed]
    public Guid EntityId { get; set; }

    public EventOperation Operation { get; set; }

    [NotNull]
    public byte[] EncryptedPayload { get; set; } = Array.Empty<byte>();

    [NotNull]
    public byte[] Nonce { get; set; } = Array.Empty<byte>();

    [Indexed]
    public long LamportClock { get; set; }

    [MaxLength(128), NotNull]
    public string AuthorDeviceId { get; set; } = string.Empty;

    [NotNull]
    public byte[] Signature { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

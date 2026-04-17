using SQLite;

namespace LocalWallet.Models;

[Table("ChatMessages")]
public class ChatMessage
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, NotNull]
    public Guid FamilyId { get; set; }

    [MaxLength(128), NotNull]
    public string AuthorDeviceId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string AuthorDisplayName { get; set; } = string.Empty;

    [MaxLength(2000), NotNull]
    public string Text { get; set; } = string.Empty;

    [Indexed]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public long LamportClock { get; set; }

    public bool IsDeleted { get; set; }

    [MaxLength(128)]
    public string? LastModifiedBy { get; set; }
}

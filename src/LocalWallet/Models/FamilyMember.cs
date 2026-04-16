using SQLite;

namespace LocalWallet.Models;

[Table("FamilyMembers")]
public class FamilyMember
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, NotNull]
    public Guid FamilyId { get; set; }

    [MaxLength(128), NotNull]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Role { get; set; } = "Member";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

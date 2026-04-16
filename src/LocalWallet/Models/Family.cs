using SQLite;

namespace LocalWallet.Models;

[Table("Families")]
public class Family
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(100), NotNull]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Role { get; set; } = "Member";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAt { get; set; }
    public bool IsDeleted { get; set; }
}

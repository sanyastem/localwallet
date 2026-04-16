using SQLite;

namespace LocalWallet.Models;

[Table("Accounts")]
public class Account
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? FamilyId { get; set; }

    [MaxLength(100), NotNull]
    public string Name { get; set; } = string.Empty;

    [MaxLength(3), NotNull]
    public string Currency { get; set; } = "PLN";

    public decimal InitialBalance { get; set; }

    [MaxLength(50)]
    public string IconName { get; set; } = "wallet";

    [MaxLength(9)]
    public string ColorHex { get; set; } = "#2E7D32";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; }

    public long LamportClock { get; set; }

    [MaxLength(128)]
    public string? LastModifiedBy { get; set; }
}

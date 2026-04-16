using SQLite;

namespace LocalWallet.Models;

[Table("Transactions")]
public class Transaction
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? FamilyId { get; set; }

    [Indexed]
    public Guid AccountId { get; set; }

    [Indexed]
    public Guid CategoryId { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(3), NotNull]
    public string Currency { get; set; } = "PLN";

    [Indexed]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Note { get; set; }

    public decimal ExchangeRateToBase { get; set; } = 1.0m;

    [MaxLength(128)]
    public string? CreatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public long LamportClock { get; set; }

    [MaxLength(128)]
    public string? LastModifiedBy { get; set; }
}

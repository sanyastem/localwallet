using SQLite;

namespace LocalWallet.Models;

public enum CategoryType
{
    Expense = 0,
    Income = 1
}

[Table("Categories")]
public class Category
{
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? FamilyId { get; set; }

    [MaxLength(100), NotNull]
    public string Name { get; set; } = string.Empty;

    public CategoryType Type { get; set; } = CategoryType.Expense;

    [MaxLength(50)]
    public string IconName { get; set; } = "label";

    [MaxLength(9)]
    public string ColorHex { get; set; } = "#1976D2";

    public bool IsDeleted { get; set; }

    public long LamportClock { get; set; }

    [MaxLength(128)]
    public string? LastModifiedBy { get; set; }
}

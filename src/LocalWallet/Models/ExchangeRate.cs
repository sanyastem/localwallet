using SQLite;

namespace LocalWallet.Models;

[Table("ExchangeRates")]
public class ExchangeRate
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(3), NotNull, Indexed(Name = "idx_rate_pair", Order = 1)]
    public string BaseCurrency { get; set; } = "PLN";

    [MaxLength(3), NotNull, Indexed(Name = "idx_rate_pair", Order = 2)]
    public string TargetCurrency { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(30)]
    public string Source { get; set; } = "NBP";
}

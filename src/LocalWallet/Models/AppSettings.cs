using SQLite;

namespace LocalWallet.Models;

[Table("AppSettings")]
public class AppSettings
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    [MaxLength(3)]
    public string BaseCurrency { get; set; } = "PLN";

    [MaxLength(200)]
    public string DisplayCurrenciesCsv { get; set; } = "PLN,EUR,USD";

    public bool BiometricEnabled { get; set; }

    public DateTime? LastRatesUpdate { get; set; }

    public bool IsOnboarded { get; set; }

    public long CurrentLamportClock { get; set; }
}

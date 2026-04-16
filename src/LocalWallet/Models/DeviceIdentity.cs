using SQLite;

namespace LocalWallet.Models;

[Table("DeviceIdentity")]
public class DeviceIdentity
{
    [PrimaryKey]
    public int Id { get; set; } = 1;

    [MaxLength(128), NotNull]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DisplayName { get; set; } = "Моё устройство";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

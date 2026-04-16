namespace LocalWallet.Models;

public record CurrencyInfo(string Code, string Name, string Flag)
{
    public string Display => $"{Flag}  {Code} — {Name}";
}

public static class SupportedCurrencies
{
    public static readonly IReadOnlyList<CurrencyInfo> All = new List<CurrencyInfo>
    {
        new("PLN", "Польский злотый", "🇵🇱"),
        new("EUR", "Евро", "🇪🇺"),
        new("USD", "Доллар США", "🇺🇸"),
        new("GBP", "Фунт стерлингов", "🇬🇧"),
        new("CHF", "Швейцарский франк", "🇨🇭"),
        new("BYN", "Белорусский рубль", "🇧🇾"),
        new("UAH", "Гривна", "🇺🇦"),
        new("RUB", "Российский рубль", "🇷🇺"),
        new("CZK", "Чешская крона", "🇨🇿"),
        new("HUF", "Венгерский форинт", "🇭🇺"),
        new("SEK", "Шведская крона", "🇸🇪"),
        new("NOK", "Норвежская крона", "🇳🇴"),
        new("DKK", "Датская крона", "🇩🇰"),
        new("JPY", "Японская иена", "🇯🇵"),
        new("CNY", "Китайский юань", "🇨🇳"),
        new("CAD", "Канадский доллар", "🇨🇦"),
        new("AUD", "Австралийский доллар", "🇦🇺"),
        new("TRY", "Турецкая лира", "🇹🇷"),
    };

    public static CurrencyInfo? Find(string code) =>
        All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
}

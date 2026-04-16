using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LocalWallet.Models;

namespace LocalWallet.Services.ExchangeRates;

public class NbpRatesProvider
{
    private readonly IHttpClientFactory _httpFactory;
    private const string Endpoint = "https://api.nbp.pl/api/exchangerates/tables/A?format=json";

    public NbpRatesProvider(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<IReadOnlyList<ExchangeRate>?> FetchAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var tables = await client.GetFromJsonAsync<List<NbpTable>>(Endpoint, ct);
            if (tables is null || tables.Count == 0) return null;

            var table = tables[0];
            var now = DateTime.UtcNow;
            var list = new List<ExchangeRate>
            {
                new() { BaseCurrency = "PLN", TargetCurrency = "PLN", Rate = 1m, FetchedAt = now, Source = "NBP" }
            };

            foreach (var r in table.Rates ?? Enumerable.Empty<NbpRate>())
            {
                if (string.IsNullOrEmpty(r.Code)) continue;
                list.Add(new ExchangeRate
                {
                    BaseCurrency = "PLN",
                    TargetCurrency = r.Code,
                    Rate = r.Mid == 0 ? 1m : 1m / r.Mid,
                    FetchedAt = now,
                    Source = "NBP"
                });
            }

            return list;
        }
        catch
        {
            return null;
        }
    }

    private sealed class NbpTable
    {
        [JsonPropertyName("table")] public string? Table { get; set; }
        [JsonPropertyName("no")] public string? No { get; set; }
        [JsonPropertyName("effectiveDate")] public string? EffectiveDate { get; set; }
        [JsonPropertyName("rates")] public List<NbpRate>? Rates { get; set; }
    }

    private sealed class NbpRate
    {
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("mid")] public decimal Mid { get; set; }
    }
}

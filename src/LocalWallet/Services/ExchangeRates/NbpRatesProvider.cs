using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LocalWallet.Models;

namespace LocalWallet.Services.ExchangeRates;

public class NbpRatesProvider
{
    private readonly IHttpClientFactory _httpFactory;
    private const string TableA = "https://api.nbp.pl/api/exchangerates/tables/A?format=json";
    private const string TableB = "https://api.nbp.pl/api/exchangerates/tables/B?format=json";

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

            var tasks = new[] { TableA, TableB }
                .Select(url => client.GetFromJsonAsync<List<NbpTable>>(url, ct))
                .ToArray();
            var tables = (await Task.WhenAll(tasks))
                .Where(t => t is { Count: > 0 })
                .SelectMany(t => t![0].Rates ?? Enumerable.Empty<NbpRate>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Code) && r.Mid > 0)
                .GroupBy(r => r.Code!.ToUpperInvariant())
                .Select(g => g.First())
                .ToList();

            if (tables.Count == 0) return null;

            var now = DateTime.UtcNow;
            var list = new List<ExchangeRate>
            {
                new() { BaseCurrency = "PLN", TargetCurrency = "PLN", Rate = 1m, FetchedAt = now, Source = "NBP" }
            };

            foreach (var r in tables)
            {
                list.Add(new ExchangeRate
                {
                    BaseCurrency = "PLN",
                    TargetCurrency = r.Code!.ToUpperInvariant(),
                    Rate = 1m / r.Mid,
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

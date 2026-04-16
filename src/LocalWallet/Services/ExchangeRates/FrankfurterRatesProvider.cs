using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LocalWallet.Models;

namespace LocalWallet.Services.ExchangeRates;

public class FrankfurterRatesProvider
{
    private readonly IHttpClientFactory _httpFactory;

    public FrankfurterRatesProvider(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<IReadOnlyList<ExchangeRate>?> FetchAsync(string baseCurrency, CancellationToken ct = default)
    {
        try
        {
            using var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            var url = $"https://api.frankfurter.app/latest?from={baseCurrency}";
            var response = await client.GetFromJsonAsync<FrankfurterResponse>(url, ct);
            if (response?.Rates is null) return null;

            var now = DateTime.UtcNow;
            var list = new List<ExchangeRate>
            {
                new() { BaseCurrency = baseCurrency, TargetCurrency = baseCurrency, Rate = 1m, FetchedAt = now, Source = "Frankfurter" }
            };

            foreach (var (code, value) in response.Rates)
            {
                list.Add(new ExchangeRate
                {
                    BaseCurrency = baseCurrency,
                    TargetCurrency = code,
                    Rate = value,
                    FetchedAt = now,
                    Source = "Frankfurter"
                });
            }

            return list;
        }
        catch
        {
            return null;
        }
    }

    private sealed class FrankfurterResponse
    {
        [JsonPropertyName("base")] public string? Base { get; set; }
        [JsonPropertyName("date")] public string? Date { get; set; }
        [JsonPropertyName("rates")] public Dictionary<string, decimal>? Rates { get; set; }
    }
}

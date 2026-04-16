using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LocalWallet.Models;
using LocalWallet.Services.Database;

namespace LocalWallet.Services;

public interface IExportService
{
    Task<string> ExportTransactionsCsvAsync(DateTime? from = null, DateTime? to = null);
    Task ShareExportAsync(string filePath);
}

public class ExportService : IExportService
{
    private readonly IDatabaseService _db;

    public ExportService(IDatabaseService db) => _db = db;

    public async Task<string> ExportTransactionsCsvAsync(DateTime? from = null, DateTime? to = null)
    {
        var transactions = from.HasValue && to.HasValue
            ? await _db.GetTransactionsInRangeAsync(from.Value, to.Value)
            : await _db.GetTransactionsAsync();

        var accounts = (await _db.GetAccountsAsync()).ToDictionary(a => a.Id, a => a.Name);
        var categories = (await _db.GetCategoriesAsync()).ToDictionary(c => c.Id, c => c.Name);

        var rows = transactions.Select(t => new CsvRow
        {
            Date = t.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
            Account = accounts.GetValueOrDefault(t.AccountId, "(удалён)"),
            Category = categories.GetValueOrDefault(t.CategoryId, "(удалена)"),
            Amount = t.Amount,
            Currency = t.Currency,
            Note = t.Note ?? string.Empty
        }).ToList();

        var fileName = $"localwallet_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        await using var writer = new StreamWriter(filePath);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "," };
        using var csv = new CsvWriter(writer, config);
        await csv.WriteRecordsAsync(rows);

        return filePath;
    }

    public async Task ShareExportAsync(string filePath)
    {
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Экспорт LocalWallet",
            File = new ShareFile(filePath)
        });
    }

    private sealed class CsvRow
    {
        public string Date { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }
}

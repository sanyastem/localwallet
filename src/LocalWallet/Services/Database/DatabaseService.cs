using LocalWallet.Models;
using SQLite;

namespace LocalWallet.Services.Database;

public class DatabaseService : IDatabaseService
{
    private const string DbFileName = "localwallet.db3";
    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache |
        SQLiteOpenFlags.FullMutex;

    private SQLiteAsyncConnection? _db;
    private bool _initialized;

    private static string DbPath =>
        Path.Combine(FileSystem.AppDataDirectory, DbFileName);

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_db is not null && _initialized) return _db;
        await InitializeAsync();
        return _db!;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        _db = new SQLiteAsyncConnection(DbPath, Flags);
        await _db.CreateTableAsync<Account>();
        await _db.CreateTableAsync<Category>();
        await _db.CreateTableAsync<Transaction>();
        await _db.CreateTableAsync<ExchangeRate>();
        await _db.CreateTableAsync<AppSettings>();

        var settings = await _db.FindAsync<AppSettings>(1);
        if (settings is null)
        {
            await _db.InsertAsync(new AppSettings());
            await SeedDefaultCategoriesAsync(_db);
        }

        _initialized = true;
    }

    private static async Task SeedDefaultCategoriesAsync(SQLiteAsyncConnection db)
    {
        var defaults = new[]
        {
            new Category { Name = "Еда", Type = CategoryType.Expense, IconName = "restaurant", ColorHex = "#EF5350" },
            new Category { Name = "Транспорт", Type = CategoryType.Expense, IconName = "commute", ColorHex = "#42A5F5" },
            new Category { Name = "Жильё", Type = CategoryType.Expense, IconName = "home", ColorHex = "#8D6E63" },
            new Category { Name = "Развлечения", Type = CategoryType.Expense, IconName = "movie", ColorHex = "#AB47BC" },
            new Category { Name = "Здоровье", Type = CategoryType.Expense, IconName = "health", ColorHex = "#26A69A" },
            new Category { Name = "Зарплата", Type = CategoryType.Income, IconName = "salary", ColorHex = "#66BB6A" },
            new Category { Name = "Подарок", Type = CategoryType.Income, IconName = "gift", ColorHex = "#FFA726" }
        };
        foreach (var c in defaults) await db.InsertAsync(c);
    }

    // --- Accounts ---

    public async Task<List<Account>> GetAccountsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Account>().Where(a => !a.IsDeleted).ToListAsync();
    }

    public async Task<Account?> GetAccountAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        return await db.FindAsync<Account>(id);
    }

    public async Task SaveAccountAsync(Account account)
    {
        var db = await GetConnectionAsync();
        var existing = await db.FindAsync<Account>(account.Id);
        if (existing is null) await db.InsertAsync(account);
        else await db.UpdateAsync(account);
    }

    public async Task DeleteAccountAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        var acc = await db.FindAsync<Account>(id);
        if (acc is null) return;
        acc.IsDeleted = true;
        await db.UpdateAsync(acc);
    }

    // --- Categories ---

    public async Task<List<Category>> GetCategoriesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Category>().Where(c => !c.IsDeleted).ToListAsync();
    }

    public async Task<Category?> GetCategoryAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        return await db.FindAsync<Category>(id);
    }

    public async Task SaveCategoryAsync(Category category)
    {
        var db = await GetConnectionAsync();
        var existing = await db.FindAsync<Category>(category.Id);
        if (existing is null) await db.InsertAsync(category);
        else await db.UpdateAsync(category);
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        var cat = await db.FindAsync<Category>(id);
        if (cat is null) return;
        cat.IsDeleted = true;
        await db.UpdateAsync(cat);
    }

    // --- Transactions ---

    public async Task<List<Transaction>> GetTransactionsAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Transaction>()
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetTransactionsInRangeAsync(DateTime from, DateTime to)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Transaction>()
            .Where(t => !t.IsDeleted && t.Date >= from && t.Date <= to)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<Transaction?> GetTransactionAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        return await db.FindAsync<Transaction>(id);
    }

    public async Task SaveTransactionAsync(Transaction transaction)
    {
        var db = await GetConnectionAsync();
        var existing = await db.FindAsync<Transaction>(transaction.Id);
        if (existing is null) await db.InsertAsync(transaction);
        else await db.UpdateAsync(transaction);
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        var tx = await db.FindAsync<Transaction>(id);
        if (tx is null) return;
        tx.IsDeleted = true;
        await db.UpdateAsync(tx);
    }

    public async Task<int> DeleteTransactionsInRangeAsync(DateTime from, DateTime to)
    {
        var db = await GetConnectionAsync();
        var items = await db.Table<Transaction>()
            .Where(t => !t.IsDeleted && t.Date >= from && t.Date <= to)
            .ToListAsync();
        foreach (var t in items)
        {
            t.IsDeleted = true;
            await db.UpdateAsync(t);
        }
        return items.Count;
    }

    // --- Exchange rates ---

    public async Task<List<ExchangeRate>> GetLatestRatesAsync(string baseCurrency)
    {
        var db = await GetConnectionAsync();
        return await db.Table<ExchangeRate>()
            .Where(r => r.BaseCurrency == baseCurrency)
            .ToListAsync();
    }

    public async Task SaveRatesAsync(IEnumerable<ExchangeRate> rates)
    {
        var db = await GetConnectionAsync();
        foreach (var rate in rates)
        {
            var existing = await db.Table<ExchangeRate>()
                .Where(r => r.BaseCurrency == rate.BaseCurrency && r.TargetCurrency == rate.TargetCurrency)
                .FirstOrDefaultAsync();
            if (existing is null)
            {
                await db.InsertAsync(rate);
            }
            else
            {
                existing.Rate = rate.Rate;
                existing.FetchedAt = rate.FetchedAt;
                existing.Source = rate.Source;
                await db.UpdateAsync(existing);
            }
        }
    }

    public async Task<ExchangeRate?> GetRateAsync(string baseCurrency, string targetCurrency)
    {
        if (baseCurrency == targetCurrency)
            return new ExchangeRate { BaseCurrency = baseCurrency, TargetCurrency = targetCurrency, Rate = 1m };

        var db = await GetConnectionAsync();
        return await db.Table<ExchangeRate>()
            .Where(r => r.BaseCurrency == baseCurrency && r.TargetCurrency == targetCurrency)
            .FirstOrDefaultAsync();
    }

    // --- Settings ---

    public async Task<AppSettings> GetSettingsAsync()
    {
        var db = await GetConnectionAsync();
        var s = await db.FindAsync<AppSettings>(1);
        if (s is null)
        {
            s = new AppSettings();
            await db.InsertAsync(s);
        }
        return s;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var db = await GetConnectionAsync();
        settings.Id = 1;
        await db.InsertOrReplaceAsync(settings);
    }

    public async Task ResetAllDataAsync()
    {
        var db = await GetConnectionAsync();
        await db.DeleteAllAsync<Transaction>();
        await db.DeleteAllAsync<Account>();
        await db.DeleteAllAsync<Category>();
        await db.DeleteAllAsync<ExchangeRate>();
        await db.DeleteAllAsync<AppSettings>();
        await db.InsertAsync(new AppSettings());
        await SeedDefaultCategoriesAsync(db);
    }
}

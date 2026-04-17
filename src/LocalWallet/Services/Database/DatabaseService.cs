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
        await _db.CreateTableAsync<DeviceIdentity>();
        await _db.CreateTableAsync<Family>();
        await _db.CreateTableAsync<FamilyMember>();
        await _db.CreateTableAsync<SyncEvent>();
        await _db.CreateTableAsync<ChatMessage>();

        var settings = await _db.FindAsync<AppSettings>(1);
        if (settings is null)
        {
            await _db.InsertAsync(new AppSettings());
            await SeedDefaultCategoriesAsync(_db);
        }
        else if (!settings.IsOnboarded && settings.LastRatesUpdate.HasValue)
        {
            settings.IsOnboarded = true;
            await _db.UpdateAsync(settings);
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

    // --- Device identity ---

    public async Task<DeviceIdentity?> GetDeviceIdentityAsync()
    {
        var db = await GetConnectionAsync();
        return await db.FindAsync<DeviceIdentity>(1);
    }

    public async Task SaveDeviceIdentityAsync(DeviceIdentity identity)
    {
        var db = await GetConnectionAsync();
        identity.Id = 1;
        await db.InsertOrReplaceAsync(identity);
    }

    // --- Families ---

    public async Task<List<Family>> GetFamiliesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Family>().Where(f => !f.IsDeleted).ToListAsync();
    }

    public async Task<Family?> GetFamilyAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        return await db.FindAsync<Family>(id);
    }

    public async Task SaveFamilyAsync(Family family)
    {
        var db = await GetConnectionAsync();
        await db.InsertOrReplaceAsync(family);
    }

    public async Task DeleteFamilyAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        var f = await db.FindAsync<Family>(id);
        if (f is null) return;
        f.IsDeleted = true;
        await db.UpdateAsync(f);
    }

    public async Task<List<FamilyMember>> GetFamilyMembersAsync(Guid familyId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<FamilyMember>()
            .Where(m => m.FamilyId == familyId && m.RevokedAt == null)
            .ToListAsync();
    }

    public async Task SaveFamilyMemberAsync(FamilyMember member)
    {
        var db = await GetConnectionAsync();
        var existing = await db.FindAsync<FamilyMember>(member.Id);
        if (existing is null) await db.InsertAsync(member);
        else await db.UpdateAsync(member);
    }

    public async Task<FamilyMember?> FindFamilyMemberAsync(Guid familyId, string deviceId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<FamilyMember>()
            .Where(m => m.FamilyId == familyId && m.DeviceId == deviceId)
            .FirstOrDefaultAsync();
    }

    // --- Chat messages ---

    public async Task<List<ChatMessage>> GetChatMessagesAsync(Guid familyId, int limit = 500)
    {
        var db = await GetConnectionAsync();
        return await db.Table<ChatMessage>()
            .Where(m => m.FamilyId == familyId && !m.IsDeleted)
            .OrderBy(m => m.SentAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<ChatMessage?> GetChatMessageAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        return await db.FindAsync<ChatMessage>(id);
    }

    public async Task SaveChatMessageAsync(ChatMessage message)
    {
        var db = await GetConnectionAsync();
        var existing = await db.FindAsync<ChatMessage>(message.Id);
        if (existing is null) await db.InsertAsync(message);
        else await db.UpdateAsync(message);
    }

    public async Task DeleteChatMessageAsync(Guid id)
    {
        var db = await GetConnectionAsync();
        var m = await db.FindAsync<ChatMessage>(id);
        if (m is null) return;
        m.IsDeleted = true;
        await db.UpdateAsync(m);
    }

    // --- Sync events ---

    public async Task<List<SyncEvent>> GetEventsSinceAsync(Guid familyId, string deviceId, long sinceClock)
    {
        var db = await GetConnectionAsync();
        return await db.Table<SyncEvent>()
            .Where(e => e.FamilyId == familyId && e.AuthorDeviceId == deviceId && e.LamportClock > sinceClock)
            .OrderBy(e => e.LamportClock)
            .ToListAsync();
    }

    public async Task<long> GetMaxClockForDeviceAsync(Guid familyId, string deviceId)
    {
        var db = await GetConnectionAsync();
        var latest = await db.Table<SyncEvent>()
            .Where(e => e.FamilyId == familyId && e.AuthorDeviceId == deviceId)
            .OrderByDescending(e => e.LamportClock)
            .FirstOrDefaultAsync();
        return latest?.LamportClock ?? 0;
    }

    public async Task<List<(string DeviceId, long MaxClock)>> GetVectorClockAsync(Guid familyId)
    {
        var db = await GetConnectionAsync();
        var rows = await db.QueryAsync<VectorRow>(
            "SELECT AuthorDeviceId AS DeviceId, MAX(LamportClock) AS MaxClock FROM SyncEvents WHERE FamilyId = ? GROUP BY AuthorDeviceId",
            familyId);
        return rows.Select(r => (r.DeviceId, r.MaxClock)).ToList();
    }

    private class VectorRow
    {
        public string DeviceId { get; set; } = string.Empty;
        public long MaxClock { get; set; }
    }

    public async Task AppendEventAsync(SyncEvent ev)
    {
        var db = await GetConnectionAsync();
        await db.InsertOrReplaceAsync(ev);
    }

    public async Task<bool> EventExistsAsync(Guid eventId)
    {
        var db = await GetConnectionAsync();
        var e = await db.FindAsync<SyncEvent>(eventId);
        return e is not null;
    }

    public async Task ResetAllDataAsync()
    {
        var db = await GetConnectionAsync();
        await db.DeleteAllAsync<Transaction>();
        await db.DeleteAllAsync<Account>();
        await db.DeleteAllAsync<Category>();
        await db.DeleteAllAsync<ChatMessage>();
        await db.DeleteAllAsync<ExchangeRate>();
        await db.DeleteAllAsync<AppSettings>();
        // Intentionally keep DeviceIdentity: device keypair is tied to SecureStorage
        // and regenerating on reset would orphan any family memberships.
        await db.InsertAsync(new AppSettings());
        await SeedDefaultCategoriesAsync(db);
    }
}

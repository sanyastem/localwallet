using LocalWallet.Models;

namespace LocalWallet.Services.Database;

public interface IDatabaseService
{
    Task InitializeAsync();

    // Accounts
    Task<List<Account>> GetAccountsAsync();
    Task<Account?> GetAccountAsync(Guid id);
    Task SaveAccountAsync(Account account);
    Task DeleteAccountAsync(Guid id);

    // Categories
    Task<List<Category>> GetCategoriesAsync();
    Task<Category?> GetCategoryAsync(Guid id);
    Task SaveCategoryAsync(Category category);
    Task DeleteCategoryAsync(Guid id);

    // Transactions
    Task<List<Transaction>> GetTransactionsAsync();
    Task<List<Transaction>> GetTransactionsInRangeAsync(DateTime from, DateTime to);
    Task<Transaction?> GetTransactionAsync(Guid id);
    Task SaveTransactionAsync(Transaction transaction);
    Task DeleteTransactionAsync(Guid id);
    Task<int> DeleteTransactionsInRangeAsync(DateTime from, DateTime to);

    // Exchange rates
    Task<List<ExchangeRate>> GetLatestRatesAsync(string baseCurrency);
    Task SaveRatesAsync(IEnumerable<ExchangeRate> rates);
    Task<ExchangeRate?> GetRateAsync(string baseCurrency, string targetCurrency);

    // Settings
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);

    // Admin
    Task ResetAllDataAsync();
}

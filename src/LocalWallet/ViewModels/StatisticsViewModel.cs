using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services.Database;
using LocalWallet.Services.ExchangeRates;
using LocalWallet.ViewModels.Base;
using Microcharts;
using SkiaSharp;

namespace LocalWallet.ViewModels;

public partial class StatisticsViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly IExchangeRateService _rates;

    [ObservableProperty] private DateTime fromDate = DateTime.Today.AddMonths(-1);
    [ObservableProperty] private DateTime toDate = DateTime.Today.AddDays(1).AddSeconds(-1);
    [ObservableProperty] private Chart? expensesChart;
    [ObservableProperty] private decimal totalExpenses;
    [ObservableProperty] private decimal totalIncome;
    [ObservableProperty] private string baseCurrency = "PLN";
    [ObservableProperty] private ObservableCollection<CategoryStat> categoryStats = new();

    public StatisticsViewModel(IDatabaseService db, IExchangeRateService rates)
    {
        _db = db;
        _rates = rates;
        Title = "Статистика";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _db.GetSettingsAsync();
            BaseCurrency = settings.BaseCurrency;

            var transactions = await _db.GetTransactionsInRangeAsync(
                FromDate.ToUniversalTime(), ToDate.ToUniversalTime());
            var categories = (await _db.GetCategoriesAsync()).ToDictionary(c => c.Id);

            decimal expenses = 0, income = 0;
            var byCategory = new Dictionary<Guid, decimal>();
            var rateCache = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in transactions)
            {
                if (!categories.TryGetValue(t.CategoryId, out var cat)) continue;

                decimal amountInBase;
                if (string.Equals(t.Currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
                    amountInBase = Math.Abs(t.Amount);
                else if (t.ExchangeRateToBase > 0)
                    amountInBase = Math.Abs(t.Amount) * t.ExchangeRateToBase;
                else
                {
                    if (!rateCache.TryGetValue(t.Currency, out var rate))
                    {
                        rate = await _rates.ConvertAsync(1m, t.Currency, BaseCurrency);
                        rateCache[t.Currency] = rate;
                    }
                    amountInBase = Math.Abs(t.Amount) * rate;
                }

                if (cat.Type == CategoryType.Expense)
                {
                    expenses += amountInBase;
                    byCategory.TryGetValue(t.CategoryId, out var cur);
                    byCategory[t.CategoryId] = cur + amountInBase;
                }
                else
                {
                    income += amountInBase;
                }
            }

            TotalExpenses = expenses;
            TotalIncome = income;

            CategoryStats.Clear();
            var entries = new List<ChartEntry>();
            foreach (var kv in byCategory.OrderByDescending(kv => kv.Value))
            {
                if (!categories.TryGetValue(kv.Key, out var cat)) continue;
                var color = SKColor.TryParse(cat.ColorHex, out var c) ? c : SKColors.Gray;

                entries.Add(new ChartEntry((float)kv.Value)
                {
                    Label = cat.Name,
                    ValueLabel = $"{kv.Value:0.00}",
                    Color = color
                });
                CategoryStats.Add(new CategoryStat
                {
                    Name = cat.Name,
                    Amount = kv.Value,
                    Currency = BaseCurrency,
                    ColorHex = cat.ColorHex
                });
            }

            ExpensesChart = new DonutChart
            {
                Entries = entries,
                LabelTextSize = 28,
                BackgroundColor = SKColors.Transparent
            };
        }
        catch { }
        finally
        {
            IsBusy = false;
        }
    }
}

public class CategoryStat
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#888888";
    public string AmountFormatted => $"{Amount:0.00} {Currency}";
}

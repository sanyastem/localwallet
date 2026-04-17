using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services;
using LocalWallet.Services.Database;
using LocalWallet.Services.Families;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

public partial class CategoriesViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly IEntityWriter _writer;
    private readonly IFamilyService _families;

    [ObservableProperty] private ObservableCollection<Category> items = new();
    [ObservableProperty] private ObservableCollection<FamilyScope> availableScopes = new();
    [ObservableProperty] private string newCategoryName = string.Empty;
    [ObservableProperty] private FamilyScope? selectedScope;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpenseBg))]
    [NotifyPropertyChangedFor(nameof(ExpenseText))]
    [NotifyPropertyChangedFor(nameof(IncomeBg))]
    [NotifyPropertyChangedFor(nameof(IncomeText))]
    private bool newIsExpense = true;

    public Color ExpenseBg => NewIsExpense ? Color.FromArgb("#D32F2F") : Colors.Transparent;
    public Color ExpenseText => NewIsExpense ? Colors.White : Color.FromArgb("#D32F2F");
    public Color IncomeBg => !NewIsExpense ? Color.FromArgb("#2E7D32") : Colors.Transparent;
    public Color IncomeText => !NewIsExpense ? Colors.White : Color.FromArgb("#2E7D32");

    [RelayCommand] private void SetExpense() => NewIsExpense = true;
    [RelayCommand] private void SetIncome() => NewIsExpense = false;

    public CategoriesViewModel(IDatabaseService db, IEntityWriter writer, IFamilyService families)
    {
        _db = db;
        _writer = writer;
        _families = families;
        Title = "Категории";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            AvailableScopes.Clear();
            AvailableScopes.Add(new FamilyScope(null, "Личный"));
            foreach (var f in await _families.ListAsync())
                AvailableScopes.Add(new FamilyScope(f.Id, $"Семья «{f.Name}»"));
            SelectedScope ??= AvailableScopes[0];

            var list = await _db.GetCategoriesAsync();
            Items.Clear();
            foreach (var c in list.OrderBy(c => c.Type).ThenBy(c => c.Name)) Items.Add(c);
        }
        catch { }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(NewCategoryName))
        {
            await UiAlerts.ShowAsync("Проверка", "Введите название категории.");
            return;
        }
        IsBusy = true;
        try
        {
            var cat = new Category
            {
                Name = NewCategoryName.Trim(),
                Type = NewIsExpense ? CategoryType.Expense : CategoryType.Income,
                FamilyId = SelectedScope?.FamilyId
            };
            await _writer.SaveCategoryAsync(cat);
            NewCategoryName = string.Empty;
            await LoadAsync();
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync(Category category)
    {
        if (category is null) return;
        try
        {
            await _writer.DeleteCategoryAsync(category);
            Items.Remove(category);
        }
        catch (Exception ex) { await UiAlerts.ShowAsync("Ошибка", ex.Message); }
    }
}

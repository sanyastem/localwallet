using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalWallet.Models;
using LocalWallet.Services.Database;
using LocalWallet.ViewModels.Base;

namespace LocalWallet.ViewModels;

public partial class CategoriesViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;

    [ObservableProperty] private ObservableCollection<Category> items = new();
    [ObservableProperty] private string newCategoryName = string.Empty;
    [ObservableProperty] private bool newIsExpense = true;

    public CategoriesViewModel(IDatabaseService db)
    {
        _db = db;
        Title = "Категории";
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var list = await _db.GetCategoriesAsync();
        Items.Clear();
        foreach (var c in list.OrderBy(c => c.Type).ThenBy(c => c.Name)) Items.Add(c);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName)) return;
        var cat = new Category
        {
            Name = NewCategoryName.Trim(),
            Type = NewIsExpense ? CategoryType.Expense : CategoryType.Income
        };
        await _db.SaveCategoryAsync(cat);
        NewCategoryName = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(Category category)
    {
        if (category is null) return;
        await _db.DeleteCategoryAsync(category.Id);
        Items.Remove(category);
    }
}

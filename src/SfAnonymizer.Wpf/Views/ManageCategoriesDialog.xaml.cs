using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SfAnonymizer.Core.Models;
using SfAnonymizer.Wpf.ViewModels;

namespace SfAnonymizer.Wpf.Views;

public partial class ManageCategoriesDialog : Window
{
    private readonly ObservableCollection<CategoryOption> _allCategories;

    /// <summary>
    /// Custom categories exposed for the ListBox binding.
    /// </summary>
    public ObservableCollection<CustomCategoryDefinition> CustomCategories { get; } = [];

    public CategoryOption? SelectedCategory { get; set; }

    public ManageCategoriesDialog(ObservableCollection<CategoryOption> allCategories)
    {
        _allCategories = allCategories;

        // Populate from existing custom entries
        foreach (var opt in allCategories.Where(o => o.IsCustom))
            CustomCategories.Add(opt.Custom!);

        InitializeComponent();
        DataContext = this;
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        // Prevent duplicates
        if (_allCategories.Any(o => string.Equals(o.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
        {
            NameBox.Focus();
            return;
        }

        var def = new CustomCategoryDefinition
        {
            Name = name,
            UsePrefix = UsePrefixBox.IsChecked == true,
            Prefix = PrefixBox.Text.Trim()
        };

        CustomCategories.Add(def);
        _allCategories.Add(new CategoryOption(def));

        // Reset form
        NameBox.Clear();
        UsePrefixBox.IsChecked = false;
        PrefixBox.Clear();
        NameBox.Focus();
    }

    private void RemoveCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CustomCategoryDefinition def })
        {
            CustomCategories.Remove(def);
            var opt = _allCategories.FirstOrDefault(o => o.Custom == def);
            if (opt is not null)
                _allCategories.Remove(opt);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

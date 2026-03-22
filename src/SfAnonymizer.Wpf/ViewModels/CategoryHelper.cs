using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Wpf.ViewModels;

public static class CategoryHelper
{
    /// <summary>
    /// Built-in category options — used to seed AvailableCategories on startup.
    /// </summary>
    public static IEnumerable<CategoryOption> BuiltIns =>
        Enum.GetValues<SensitiveDataCategory>().Select(c => new CategoryOption(c));
}

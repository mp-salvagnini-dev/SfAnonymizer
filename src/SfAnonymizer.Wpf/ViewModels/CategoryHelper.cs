using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Wpf.ViewModels;

public static class CategoryHelper
{
    public static SensitiveDataCategory[] All { get; } = Enum.GetValues<SensitiveDataCategory>();
}

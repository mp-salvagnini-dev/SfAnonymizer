using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Wpf.ViewModels;

/// <summary>
/// Unified category representation for the UI — wraps either a built-in
/// SensitiveDataCategory or a user-defined CustomCategoryDefinition.
/// </summary>
public sealed class CategoryOption
{
    public string DisplayName { get; }
    public SensitiveDataCategory? BuiltIn { get; }
    public CustomCategoryDefinition? Custom { get; }
    public bool IsCustom => Custom is not null;

    public CategoryOption(SensitiveDataCategory builtin)
    {
        BuiltIn = builtin;
        DisplayName = builtin.ToString();
    }

    public CategoryOption(CustomCategoryDefinition custom)
    {
        Custom = custom;
        DisplayName = custom.Name;
    }

    public override string ToString() => DisplayName;
}

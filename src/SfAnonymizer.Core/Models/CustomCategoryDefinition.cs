namespace SfAnonymizer.Core.Models;

/// <summary>
/// User-defined anonymization category with optional token prefix.
/// </summary>
public sealed class CustomCategoryDefinition
{
    public string Name { get; set; } = string.Empty;
    public bool UsePrefix { get; set; }
    public string Prefix { get; set; } = string.Empty;

    public override string ToString() => Name;
}

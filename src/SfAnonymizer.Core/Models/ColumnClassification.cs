namespace SfAnonymizer.Core.Models;

/// <summary>
/// Maps a column name to its detected sensitivity category.
/// </summary>
public sealed record ColumnClassification(
    string ColumnName,
    SensitiveDataCategory Category,
    bool IsAutoDetected,
    CustomCategoryDefinition? CustomCategory = null);

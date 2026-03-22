namespace SfAnonymizer.Core.Models;

/// <summary>
/// Result of the de-anonymization (reverse) pipeline.
/// </summary>
public sealed class DeAnonymizationResult
{
    public required List<string> Headers { get; init; }
    public required List<Dictionary<string, string>> RestoredRows { get; init; }
    public int TotalRestorations { get; init; }
    public int AffectedRows { get; init; }
    public int AffectedColumns { get; init; }
}

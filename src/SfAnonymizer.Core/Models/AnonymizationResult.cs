namespace SfAnonymizer.Core.Models;

/// <summary>
/// Result of the full anonymization pipeline.
/// </summary>
public sealed class AnonymizationResult
{
    public required List<Dictionary<string, string>> Rows { get; init; }
    public required List<string> Headers { get; init; }
    public required List<TranscodeEntry> TranscodeTable { get; init; }
    public int TotalReplacements => TranscodeTable.Count;
    public int AffectedRows => TranscodeTable.Select(t => t.RowIndex).Distinct().Count();
    public int AffectedColumns => TranscodeTable.Select(t => t.ColumnName).Distinct().Count();
}

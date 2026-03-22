namespace SfAnonymizer.Core.Models;

/// <summary>
/// Single entry in the transcode table mapping original → anonymized values.
/// </summary>
public sealed record TranscodeEntry(
    string ColumnName,
    string OriginalValue,
    string AnonymizedValue,
    string CategoryDisplay,
    int RowIndex);

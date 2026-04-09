using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Core.Services;

/// <summary>
/// Reverses the anonymization pipeline using the transcode table.
/// Uses a column-scoped strategy: each column has its own reverse map,
/// preventing token collisions across different columns.
/// </summary>
public interface IDeAnonymizationEngine
{
    DeAnonymizationResult DeAnonymize(
        List<string> headers,
        List<Dictionary<string, string>> anonymizedRows,
        List<TranscodeEntry> transcodeEntries);
}

public sealed class DeAnonymizationEngine : IDeAnonymizationEngine
{
    public DeAnonymizationResult DeAnonymize(
        List<string> headers,
        List<Dictionary<string, string>> anonymizedRows,
        List<TranscodeEntry> transcodeEntries)
    {
        // Build per-column reverse dictionaries: AnonymizedValue → OriginalValue
        // Use last-wins to handle duplicate anonymized values in the transcode table
        var reverseMapsByColumn = transcodeEntries
            .GroupBy(e => e.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var e in g)
                        map[e.AnonymizedValue] = e.OriginalValue;
                    return map;
                },
                StringComparer.OrdinalIgnoreCase);

        var restoredRows = new List<Dictionary<string, string>>();
        var restoredCells = 0;
        var affectedRowSet = new HashSet<int>();
        var affectedColumnSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var rowIdx = 0; rowIdx < anonymizedRows.Count; rowIdx++)
        {
            var originalRow = anonymizedRows[rowIdx];
            var newRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
            {
                var value = originalRow.GetValueOrDefault(header, string.Empty);

                if (!reverseMapsByColumn.TryGetValue(header, out var reverseMap))
                {
                    newRow[header] = value;
                    continue;
                }

                // Try whole-cell lookup first (standard columns)
                if (reverseMap.TryGetValue(value, out var wholeMatch))
                {
                    newRow[header] = wholeMatch;
                    restoredCells++;
                    affectedRowSet.Add(rowIdx);
                    affectedColumnSet.Add(header);
                    continue;
                }

                // Inline replacement: replace every known token found within the cell text
                var restored = value;
                var anyReplaced = false;
                foreach (var (token, original) in reverseMap)
                {
                    if (restored.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        restored = restored.Replace(token, original, StringComparison.OrdinalIgnoreCase);
                        anyReplaced = true;
                    }
                }

                newRow[header] = restored;
                if (anyReplaced)
                {
                    restoredCells++;
                    affectedRowSet.Add(rowIdx);
                    affectedColumnSet.Add(header);
                }
            }

            restoredRows.Add(newRow);
        }

        return new DeAnonymizationResult
        {
            Headers = headers,
            RestoredRows = restoredRows,
            TotalRestorations = restoredCells,
            AffectedRows = affectedRowSet.Count,
            AffectedColumns = affectedColumnSet.Count,
        };
    }
}

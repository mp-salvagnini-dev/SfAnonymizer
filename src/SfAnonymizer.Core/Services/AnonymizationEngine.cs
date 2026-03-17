using SfAnonymizer.Core.Detectors;
using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Core.Services;

/// <summary>
/// Orchestrates the full anonymization pipeline:
/// detect → classify → replace → produce transcode table.
/// </summary>
public interface IAnonymizationEngine
{
    /// <summary>
    /// Runs anonymization on the parsed data. Allows overriding detected classifications.
    /// </summary>
    AnonymizationResult Anonymize(
        List<string> headers,
        List<Dictionary<string, string>> rows,
        List<ColumnClassification>? overrideClassifications = null);
}

public sealed class AnonymizationEngine(
    ISensitiveColumnDetector detector,
    TokenGenerator tokenGenerator) : IAnonymizationEngine
{
    public AnonymizationResult Anonymize(
        List<string> headers,
        List<Dictionary<string, string>> rows,
        List<ColumnClassification>? overrideClassifications = null)
    {
        tokenGenerator.Reset();

        // Detect sensitive columns (auto + override)
        var classifications = overrideClassifications ?? detector.Classify(headers, rows);
        var classificationMap = classifications.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

        var transcodeEntries = new List<TranscodeEntry>();
        var anonymizedRows = new List<Dictionary<string, string>>();

        for (var rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            var originalRow = rows[rowIdx];
            var newRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
            {
                var value = originalRow.GetValueOrDefault(header, string.Empty);

                if (classificationMap.TryGetValue(header, out var classification)
                    && !string.IsNullOrWhiteSpace(value))
                {
                    var token = tokenGenerator.GetToken(value, classification.Category);
                    newRow[header] = token;

                    if (!string.Equals(value, token, StringComparison.Ordinal))
                    {
                        transcodeEntries.Add(new TranscodeEntry(
                            header, value, token, classification.Category, rowIdx + 1));
                    }
                }
                else
                {
                    newRow[header] = value;
                }
            }

            anonymizedRows.Add(newRow);
        }

        return new AnonymizationResult
        {
            Headers = headers,
            Rows = anonymizedRows,
            TranscodeTable = transcodeEntries
        };
    }
}

using System.Globalization;
using System.Text;
using CsvHelper;
using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Core.Services;

/// <summary>
/// Writes anonymized data and transcode tables to CSV files.
/// </summary>
public interface IFileWriter
{
    Task WriteAnonymizedCsvAsync(string outputPath, AnonymizationResult result, CancellationToken ct = default);
    Task WriteTranscodeTableAsync(string outputPath, List<TranscodeEntry> entries, CancellationToken ct = default);
}

public sealed class FileWriter : IFileWriter
{
    public async Task WriteAnonymizedCsvAsync(
        string outputPath, AnonymizationResult result, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write headers
        foreach (var header in result.Headers)
        {
            csv.WriteField(header);
        }
        await csv.NextRecordAsync();

        // Write rows
        foreach (var row in result.Rows)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var header in result.Headers)
            {
                csv.WriteField(row.GetValueOrDefault(header, string.Empty));
            }
            await csv.NextRecordAsync();
        }
    }

    public async Task WriteTranscodeTableAsync(
        string outputPath, List<TranscodeEntry> entries, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Header
        csv.WriteField("Row");
        csv.WriteField("Column");
        csv.WriteField("Category");
        csv.WriteField("Original Value");
        csv.WriteField("Anonymized Value");
        await csv.NextRecordAsync();

        foreach (var entry in entries.OrderBy(e => e.RowIndex).ThenBy(e => e.ColumnName))
        {
            ct.ThrowIfCancellationRequested();
            csv.WriteField(entry.RowIndex);
            csv.WriteField(entry.ColumnName);
            csv.WriteField(entry.Category.ToString());
            csv.WriteField(entry.OriginalValue);
            csv.WriteField(entry.AnonymizedValue);
            await csv.NextRecordAsync();
        }
    }
}

using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Core.Services;

/// <summary>
/// Writes anonymized data and transcode tables to CSV files.
/// </summary>
public interface IFileWriter
{
    Task WriteAnonymizedCsvAsync(string outputPath, AnonymizationResult result, CancellationToken ct = default);
    Task WriteAnonymizedXlsxAsync(string outputPath, AnonymizationResult result, CancellationToken ct = default);
    Task WriteTranscodeTableAsync(string outputPath, List<TranscodeEntry> entries, CancellationToken ct = default);
    Task WriteTranscodeTableXlsxAsync(string outputPath, List<TranscodeEntry> entries, CancellationToken ct = default);
    Task WriteRestoredCsvAsync(string outputPath, DeAnonymizationResult result, CancellationToken ct = default);
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

    public Task WriteAnonymizedXlsxAsync(
        string outputPath, AnonymizationResult result, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Anonymized");

        // Header row (bold)
        for (var i = 0; i < result.Headers.Count; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = result.Headers[i];
            cell.Style.Font.Bold = true;
        }

        // Data rows
        for (var rowIdx = 0; rowIdx < result.Rows.Count; rowIdx++)
        {
            ct.ThrowIfCancellationRequested();
            var row = result.Rows[rowIdx];
            for (var colIdx = 0; colIdx < result.Headers.Count; colIdx++)
                ws.Cell(rowIdx + 2, colIdx + 1).Value = row.GetValueOrDefault(result.Headers[colIdx], string.Empty);
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    public Task WriteTranscodeTableXlsxAsync(
        string outputPath, List<TranscodeEntry> entries, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Transcode");

        string[] headers = ["Row", "Column", "Category", "Original Value", "Anonymized Value"];
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }

        var sorted = entries.OrderBy(e => e.RowIndex).ThenBy(e => e.ColumnName).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var e = sorted[i];
            ws.Cell(i + 2, 1).Value = e.RowIndex;
            ws.Cell(i + 2, 2).Value = e.ColumnName;
            ws.Cell(i + 2, 3).Value = e.CategoryDisplay;
            ws.Cell(i + 2, 4).Value = e.OriginalValue;
            ws.Cell(i + 2, 5).Value = e.AnonymizedValue;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(outputPath);
        return Task.CompletedTask;
    }

    public async Task WriteRestoredCsvAsync(
        string outputPath, DeAnonymizationResult result, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        foreach (var header in result.Headers)
            csv.WriteField(header);
        await csv.NextRecordAsync();

        foreach (var row in result.RestoredRows)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var header in result.Headers)
                csv.WriteField(row.GetValueOrDefault(header, string.Empty));
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
            csv.WriteField(entry.CategoryDisplay);
            csv.WriteField(entry.OriginalValue);
            csv.WriteField(entry.AnonymizedValue);
            await csv.NextRecordAsync();
        }
    }
}

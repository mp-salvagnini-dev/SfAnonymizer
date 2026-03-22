using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Core.Services;

/// <summary>
/// Parses CSV and Excel files into a uniform row-based structure.
/// </summary>
public interface IFileParser
{
    Task<(List<string> Headers, List<Dictionary<string, string>> Rows)> ParseAsync(
        string filePath, CancellationToken ct = default);

    Task<List<TranscodeEntry>> ParseTranscodeTableAsync(
        string filePath, CancellationToken ct = default);
}

public sealed class FileParser : IFileParser
{
    public Task<(List<string> Headers, List<Dictionary<string, string>> Rows)> ParseAsync(
        string filePath, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".csv" => ParseCsvAsync(filePath, ct),
            ".xlsx" or ".xls" => Task.FromResult(ParseExcel(filePath)),
            _ => throw new NotSupportedException($"File format '{ext}' is not supported. Use .csv or .xlsx.")
        };
    }

    private static async Task<(List<string> Headers, List<Dictionary<string, string>> Rows)> ParseCsvAsync(
        string filePath, CancellationToken ct)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = csv.HeaderRecord?.ToList() ?? [];
        var rows = new List<Dictionary<string, string>>();

        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                row[header] = csv.GetField(header) ?? string.Empty;
            }
            rows.Add(row);
        }

        return (headers, rows);
    }

    public async Task<List<TranscodeEntry>> ParseTranscodeTableAsync(
        string filePath, CancellationToken ct = default)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim,
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        var entries = new List<TranscodeEntry>();
        while (await csv.ReadAsync())
        {
            ct.ThrowIfCancellationRequested();
            entries.Add(new TranscodeEntry(
                ColumnName:      csv.GetField<string>("Column")          ?? string.Empty,
                OriginalValue:   csv.GetField<string>("Original Value")  ?? string.Empty,
                AnonymizedValue: csv.GetField<string>("Anonymized Value") ?? string.Empty,
                CategoryDisplay: csv.GetField<string>("Category")        ?? string.Empty,
                RowIndex:        csv.GetField<int>("Row")));
        }

        return entries;
    }

    private static (List<string> Headers, List<Dictionary<string, string>> Rows) ParseExcel(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();
        var firstRow = sheet.FirstRowUsed() ?? throw new InvalidOperationException("Excel sheet is empty.");

        var headers = firstRow.CellsUsed()
            .Select(c => c.GetString().Trim())
            .ToList();

        var rows = new List<Dictionary<string, string>>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                dict[headers[i]] = row.Cell(i + 1).GetString().Trim();
            }
            rows.Add(dict);
        }

        return (headers, rows);
    }
}

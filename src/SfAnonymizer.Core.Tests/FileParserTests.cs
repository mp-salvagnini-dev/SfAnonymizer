using ClosedXML.Excel;
using SfAnonymizer.Core.Models;
using SfAnonymizer.Core.Services;
using Xunit;

namespace SfAnonymizer.Core.Tests;

/// <summary>
/// Integration tests for FileParser: writes real temp files and verifies parsing.
/// These tests also cover error scenarios to ensure no unhandled crashes reach the UI.
/// </summary>
public class FileParserTests : IDisposable
{
    private readonly FileParser _sut = new();
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }

    private string TempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    private string TempXlsx(Action<IXLWorksheet> populate)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
        using var wb = new XLWorkbook();
        populate(wb.Worksheets.Add("Sheet1"));
        wb.SaveAs(path);
        _tempFiles.Add(path);
        return path;
    }

    // ── CSV happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_CsvFile_ReturnsCorrectHeaders()
    {
        var path = TempCsv("Company,Status,Priority\nAcme,Open,High\n");

        var (headers, _) = await _sut.ParseAsync(path);

        Assert.Equal(["Company", "Status", "Priority"], headers);
    }

    [Fact]
    public async Task ParseAsync_CsvFile_ReturnsCorrectRowCount()
    {
        var path = TempCsv("Company,Status\nAcme,Open\nGlobex,Closed\n");

        var (_, rows) = await _sut.ParseAsync(path);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task ParseAsync_CsvFile_RowValuesAreCorrect()
    {
        var path = TempCsv("Company,Status\nAcme Corp,Open\n");

        var (_, rows) = await _sut.ParseAsync(path);

        Assert.Equal("Acme Corp", rows[0]["Company"]);
        Assert.Equal("Open",      rows[0]["Status"]);
    }

    [Fact]
    public async Task ParseAsync_CsvFile_ValuesTrimmed()
    {
        var path = TempCsv("Company,Status\n  Acme Corp  ,  Open  \n");

        var (_, rows) = await _sut.ParseAsync(path);

        Assert.Equal("Acme Corp", rows[0]["Company"]);
        Assert.Equal("Open",      rows[0]["Status"]);
    }

    [Fact]
    public async Task ParseAsync_CsvRowsAccessible_CaseInsensitive()
    {
        var path = TempCsv("Company,Status\nAcme,Open\n");

        var (_, rows) = await _sut.ParseAsync(path);

        // Row dicts use OrdinalIgnoreCase
        Assert.Equal("Acme", rows[0]["COMPANY"]);
        Assert.Equal("Acme", rows[0]["company"]);
    }

    [Fact]
    public async Task ParseAsync_CsvWithHeadersOnly_ReturnsEmptyRows()
    {
        var path = TempCsv("Company,Status\n");

        var (headers, rows) = await _sut.ParseAsync(path);

        Assert.Equal(["Company", "Status"], headers);
        Assert.Empty(rows);
    }

    // ── Excel happy path ──────────────────────────────────────────────────────

    [Fact]
    public async Task ParseAsync_ExcelFile_ReturnsCorrectHeaders()
    {
        var path = TempXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Company";
            ws.Cell(1, 2).Value = "Status";
            ws.Cell(2, 1).Value = "Acme";
            ws.Cell(2, 2).Value = "Open";
        });

        var (headers, _) = await _sut.ParseAsync(path);

        Assert.Equal(["Company", "Status"], headers);
    }

    [Fact]
    public async Task ParseAsync_ExcelFile_ReturnsCorrectRowValues()
    {
        var path = TempXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Company";
            ws.Cell(1, 2).Value = "Status";
            ws.Cell(2, 1).Value = "Acme Corp";
            ws.Cell(2, 2).Value = "Open";
        });

        var (_, rows) = await _sut.ParseAsync(path);

        Assert.Equal("Acme Corp", rows[0]["Company"]);
        Assert.Equal("Open",      rows[0]["Status"]);
    }

    // ── Transcode table parsing ────────────────────────────────────────────────

    [Fact]
    public async Task ParseTranscodeTableAsync_Csv_ReturnsCorrectEntries()
    {
        var content = "Row,Column,Category,Original Value,Anonymized Value\n" +
                      "1,Company,CustomerCompany,Acme Corp,CUST-a1b2c3\n";
        var path = TempCsv(content);

        var entries = await _sut.ParseTranscodeTableAsync(path);

        var entry = Assert.Single(entries);
        Assert.Equal(1,                  entry.RowIndex);
        Assert.Equal("Company",          entry.ColumnName);
        Assert.Equal("CustomerCompany",  entry.CategoryDisplay);
        Assert.Equal("Acme Corp",        entry.OriginalValue);
        Assert.Equal("CUST-a1b2c3",      entry.AnonymizedValue);
    }

    [Fact]
    public async Task ParseTranscodeTableAsync_Csv_MultipleEntries_AllParsed()
    {
        var content = "Row,Column,Category,Original Value,Anonymized Value\n" +
                      "1,Company,CustomerCompany,Acme,CUST-111111\n" +
                      "2,Company,CustomerCompany,Globex,CUST-222222\n";
        var path = TempCsv(content);

        var entries = await _sut.ParseTranscodeTableAsync(path);

        Assert.Equal(2, entries.Count);
    }

    // ── Error handling ─────────────────────────────────────────────────────────
    // These tests verify that FileParser throws *typed, descriptive* exceptions
    // (not NullReferenceException) so the ViewModel's catch block can surface
    // a helpful message to the user instead of crashing the app.

    [Fact]
    public async Task ParseAsync_UnsupportedExtension_ThrowsNotSupportedException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        File.WriteAllText(path, "dummy");
        _tempFiles.Add(path);

        await Assert.ThrowsAsync<NotSupportedException>(() => _sut.ParseAsync(path));
    }

    [Fact]
    public async Task ParseTranscodeTableAsync_UnsupportedExtension_ThrowsNotSupportedException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        File.WriteAllText(path, "dummy");
        _tempFiles.Add(path);

        await Assert.ThrowsAsync<NotSupportedException>(() => _sut.ParseTranscodeTableAsync(path));
    }

    [Fact]
    public async Task ParseAsync_ExcelWithEmptyFirstSheet_ThrowsInvalidOperationException()
    {
        // Simulates loading an Excel where the first sheet is blank
        // (e.g. user had a different sheet active in Excel and the first sheet has no data)
        var path = TempXlsx(_ => { /* leave sheet completely empty */ });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ParseAsync(path));
        // Verify the message is human-readable (not a cryptic NullReferenceException)
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseTranscodeTableAsync_ExcelWithEmptyFirstSheet_ThrowsInvalidOperationException()
    {
        var path = TempXlsx(_ => { });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ParseTranscodeTableAsync(path));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseTranscodeTableAsync_ExcelMissingRequiredColumn_ThrowsInvalidOperationException()
    {
        // Transcode Excel is missing the "Column" header — FileParser should throw a clear error
        var path = TempXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Row";
            // "Column" column is intentionally missing
            ws.Cell(1, 2).Value = "Category";
            ws.Cell(1, 3).Value = "Original Value";
            ws.Cell(1, 4).Value = "Anonymized Value";
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.ParseTranscodeTableAsync(path));
        Assert.Contains("Column", ex.Message);
    }

    [Fact]
    public async Task ParseAsync_FileDoesNotExist_ThrowsFileNotFoundException()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");

        await Assert.ThrowsAsync<FileNotFoundException>(() => _sut.ParseAsync(nonExistent));
    }
}

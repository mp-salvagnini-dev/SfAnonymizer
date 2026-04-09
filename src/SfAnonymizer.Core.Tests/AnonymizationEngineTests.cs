using SfAnonymizer.Core.Detectors;
using SfAnonymizer.Core.Models;
using SfAnonymizer.Core.Services;
using Xunit;

namespace SfAnonymizer.Core.Tests;

/// <summary>
/// Tests for AnonymizationEngine.
/// All tests use explicit overrideClassifications so the detector is bypassed,
/// keeping each test focused on the engine's own logic.
/// </summary>
public class AnonymizationEngineTests
{
    private static AnonymizationEngine CreateEngine() =>
        new(new SalesforceColumnDetector(), new TokenGenerator());

    private static List<string> Headers(params string[] h) => [.. h];

    private static List<Dictionary<string, string>> OneRow(params (string col, string val)[] cells)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, val) in cells)
            row[col] = val;
        return [row];
    }

    private static List<ColumnClassification> Classify(string column, SensitiveDataCategory category) =>
        [new ColumnClassification(column, category, IsAutoDetected: false)];

    // ── Core replacement behavior ─────────────────────────────────────────────

    [Fact]
    public void Anonymize_SensitiveColumn_ValueIsReplaced()
    {
        var engine = CreateEngine();
        var headers = Headers("Company", "Status");
        var rows    = OneRow(("Company", "Acme Corp"), ("Status", "Open"));

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        var replaced = result.Rows[0]["Company"];
        Assert.NotEqual("Acme Corp", replaced);
        Assert.StartsWith("CUST-", replaced);
    }

    [Fact]
    public void Anonymize_NonSensitiveColumn_ValueUnchanged()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company", "Status");
        var rows    = OneRow(("Company", "Acme Corp"), ("Status", "Open"));

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        Assert.Equal("Open", result.Rows[0]["Status"]);
    }

    [Fact]
    public void Anonymize_EmptyValue_NotReplaced()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company");
        var rows    = OneRow(("Company", ""));

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        Assert.Equal("", result.Rows[0]["Company"]);
    }

    [Fact]
    public void Anonymize_WhitespaceOnlyValue_NotReplaced()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company");
        var rows    = OneRow(("Company", "   "));

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        Assert.Equal("   ", result.Rows[0]["Company"]);
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Anonymize_SameValueInSameColumn_GetsSameToken()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company");
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "Acme Corp" },
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "Acme Corp" },
        };

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        Assert.Equal(result.Rows[0]["Company"], result.Rows[1]["Company"]);
    }

    // ── Transcode table ───────────────────────────────────────────────────────

    [Fact]
    public void Anonymize_TranscodeTable_ContainsOriginalAndAnonymizedValues()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company");
        var rows    = OneRow(("Company", "Acme Corp"));

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        var entry = Assert.Single(result.TranscodeTable);
        Assert.Equal("Company", entry.ColumnName);
        Assert.Equal("Acme Corp", entry.OriginalValue);
        Assert.StartsWith("CUST-", entry.AnonymizedValue);
    }

    [Fact]
    public void Anonymize_TranscodeTable_RowIndex_IsOneBased()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company");
        var rows    = OneRow(("Company", "Acme Corp"));

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        Assert.Equal(1, result.TranscodeTable[0].RowIndex);
    }

    [Fact]
    public void Anonymize_EmptyValue_NotIncludedInTranscodeTable()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company");
        var rows    = OneRow(("Company", ""));

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        Assert.Empty(result.TranscodeTable);
    }

    [Fact]
    public void Anonymize_MultipleRows_TranscodeTableHasOneEntryPerReplacedCell()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company", "Status");
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "Acme",  ["Status"] = "Open" },
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "Globex",["Status"] = "Closed" },
        };

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        // Only "Company" column is classified → 2 replacements
        Assert.Equal(2, result.TranscodeTable.Count);
        Assert.All(result.TranscodeTable, e => Assert.Equal("Company", e.ColumnName));
    }

    // ── Case-insensitive column matching ──────────────────────────────────────

    [Fact]
    public void Anonymize_ClassificationColumnNameCaseInsensitive_StillMatched()
    {
        var engine  = CreateEngine();
        // Header uses uppercase, classification uses mixed case
        var headers = Headers("COMPANY");
        var rows    = OneRow(("COMPANY", "Acme Corp"));
        var overrides = new List<ColumnClassification>
        {
            new("Company", SensitiveDataCategory.CustomerCompany, IsAutoDetected: false),
        };

        var result = engine.Anonymize(headers, rows, overrides);

        Assert.NotEqual("Acme Corp", result.Rows[0]["COMPANY"]);
    }

    // ── Override classifications ──────────────────────────────────────────────

    [Fact]
    public void Anonymize_OverrideClassifications_BypassAutoDetection()
    {
        // "Notes" would never be auto-detected, but we force it with an override
        var engine  = CreateEngine();
        var headers = Headers("Notes");
        var rows    = OneRow(("Notes", "some note"));
        var overrides = new List<ColumnClassification>
        {
            new("Notes", SensitiveDataCategory.PersonName, IsAutoDetected: false),
        };

        var result = engine.Anonymize(headers, rows, overrides);

        Assert.NotEqual("some note", result.Rows[0]["Notes"]);
        Assert.StartsWith("PERSON-", result.Rows[0]["Notes"]);
    }

    [Fact]
    public void Anonymize_EmptyOverrideList_NoColumnsAnonymized()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company", "Name");
        var rows    = OneRow(("Company", "Acme"), ("Name", "John"));

        var result = engine.Anonymize(headers, rows, []);

        Assert.Equal("Acme", result.Rows[0]["Company"]);
        Assert.Equal("John", result.Rows[0]["Name"]);
        Assert.Empty(result.TranscodeTable);
    }

    // ── Headers preserved ────────────────────────────────────────────────────

    [Fact]
    public void Anonymize_ResultHeaders_MatchInputHeaders()
    {
        var engine  = CreateEngine();
        var headers = Headers("Company", "Status", "Priority");
        var rows    = OneRow(("Company", "Acme"), ("Status", "Open"), ("Priority", "High"));

        var result = engine.Anonymize(headers, rows, Classify("Company", SensitiveDataCategory.CustomerCompany));

        Assert.Equal(headers, result.Headers);
    }
}

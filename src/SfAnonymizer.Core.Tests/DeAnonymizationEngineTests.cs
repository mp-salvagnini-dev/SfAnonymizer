using SfAnonymizer.Core.Models;
using SfAnonymizer.Core.Services;
using Xunit;

namespace SfAnonymizer.Core.Tests;

public class DeAnonymizationEngineTests
{
    private static readonly DeAnonymizationEngine Sut = new();

    private static List<string> Headers(params string[] h) => [.. h];

    private static List<Dictionary<string, string>> OneRow(params (string col, string val)[] cells)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, val) in cells)
            row[col] = val;
        return [row];
    }

    // ── Basic restoration ────────────────────────────────────────────────────

    [Fact]
    public void DeAnonymize_KnownToken_RestoresOriginalValue()
    {
        var headers = Headers("Company");
        var rows    = OneRow(("Company", "CUST-a1b2c3"));
        var transcode = new List<TranscodeEntry>
        {
            new("Company", "Acme Corp", "CUST-a1b2c3", "CustomerCompany", 1),
        };

        var result = Sut.DeAnonymize(headers, rows, transcode);

        Assert.Equal("Acme Corp", result.RestoredRows[0]["Company"]);
    }

    [Fact]
    public void DeAnonymize_UnknownToken_LeftAsIs()
    {
        var headers = Headers("Company");
        var rows    = OneRow(("Company", "CUST-xxxxxx"));
        var transcode = new List<TranscodeEntry>
        {
            new("Company", "Acme Corp", "CUST-a1b2c3", "CustomerCompany", 1),
        };

        var result = Sut.DeAnonymize(headers, rows, transcode);

        Assert.Equal("CUST-xxxxxx", result.RestoredRows[0]["Company"]);
    }

    [Fact]
    public void DeAnonymize_ColumnNotInTranscodeTable_LeftAsIs()
    {
        var headers = Headers("Company", "Status");
        var rows    = OneRow(("Company", "CUST-a1b2c3"), ("Status", "Open"));
        var transcode = new List<TranscodeEntry>
        {
            new("Company", "Acme Corp", "CUST-a1b2c3", "CustomerCompany", 1),
        };

        var result = Sut.DeAnonymize(headers, rows, transcode);

        Assert.Equal("Open", result.RestoredRows[0]["Status"]);
    }

    [Fact]
    public void DeAnonymize_EmptyTranscodeTable_AllValuesUnchanged()
    {
        var headers = Headers("Company", "Status");
        var rows    = OneRow(("Company", "CUST-a1b2c3"), ("Status", "Open"));

        var result = Sut.DeAnonymize(headers, rows, []);

        Assert.Equal("CUST-a1b2c3", result.RestoredRows[0]["Company"]);
        Assert.Equal("Open", result.RestoredRows[0]["Status"]);
    }

    // ── Per-column scoping ────────────────────────────────────────────────────

    [Fact]
    public void DeAnonymize_SameTokenInTwoColumns_EachRestoredToItsOwnOriginal()
    {
        // Both columns happen to produce the same anonymized token,
        // but each should restore to its own original value.
        var headers = Headers("CompanyA", "CompanyB");
        var rows    = OneRow(("CompanyA", "CUST-aabbcc"), ("CompanyB", "CUST-aabbcc"));
        var transcode = new List<TranscodeEntry>
        {
            new("CompanyA", "Acme Corp",  "CUST-aabbcc", "CustomerCompany", 1),
            new("CompanyB", "Globex Inc", "CUST-aabbcc", "CustomerCompany", 1),
        };

        var result = Sut.DeAnonymize(headers, rows, transcode);

        Assert.Equal("Acme Corp",  result.RestoredRows[0]["CompanyA"]);
        Assert.Equal("Globex Inc", result.RestoredRows[0]["CompanyB"]);
    }

    // ── Statistics ────────────────────────────────────────────────────────────

    [Fact]
    public void DeAnonymize_TotalRestorations_CountsAllRestoredCells()
    {
        var headers = Headers("Company", "Name");
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "CUST-111111", ["Name"] = "PERSON-222222" },
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "CUST-333333", ["Name"] = "PERSON-444444" },
        };
        var transcode = new List<TranscodeEntry>
        {
            new("Company", "Acme",  "CUST-111111",   "CustomerCompany", 1),
            new("Name",    "Alice", "PERSON-222222",  "PersonName",      1),
            new("Company", "Globex","CUST-333333",   "CustomerCompany", 2),
            new("Name",    "Bob",   "PERSON-444444",  "PersonName",      2),
        };

        var result = Sut.DeAnonymize(headers, rows, transcode);

        Assert.Equal(4, result.TotalRestorations);
    }

    [Fact]
    public void DeAnonymize_AffectedRows_CountsDistinctRowsWithAtLeastOneRestoration()
    {
        var headers = Headers("Company");
        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "CUST-111111" }, // restored
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "CUST-unknown" }, // not in transcode
            new(StringComparer.OrdinalIgnoreCase) { ["Company"] = "CUST-333333" }, // restored
        };
        var transcode = new List<TranscodeEntry>
        {
            new("Company", "Acme",   "CUST-111111", "CustomerCompany", 1),
            new("Company", "Globex", "CUST-333333", "CustomerCompany", 3),
        };

        var result = Sut.DeAnonymize(headers, rows, transcode);

        Assert.Equal(2, result.AffectedRows);
    }

    [Fact]
    public void DeAnonymize_AffectedColumns_CountsDistinctColumnsWithAtLeastOneRestoration()
    {
        var headers = Headers("Company", "Name", "Status");
        var rows    = OneRow(("Company", "CUST-111111"), ("Name", "PERSON-222222"), ("Status", "Open"));
        var transcode = new List<TranscodeEntry>
        {
            new("Company", "Acme",  "CUST-111111",  "CustomerCompany", 1),
            new("Name",    "Alice", "PERSON-222222", "PersonName",      1),
        };

        var result = Sut.DeAnonymize(headers, rows, transcode);

        Assert.Equal(2, result.AffectedColumns);
    }

    // ── Headers preserved ────────────────────────────────────────────────────

    [Fact]
    public void DeAnonymize_ResultHeaders_MatchInputHeaders()
    {
        var headers = Headers("Company", "Status");
        var rows    = OneRow(("Company", "CUST-111111"), ("Status", "Open"));

        var result = Sut.DeAnonymize(headers, rows, []);

        Assert.Equal(headers, result.Headers);
    }

    // ── Last-wins strategy for duplicate anonymized values ────────────────────

    [Fact]
    public void DeAnonymize_DuplicateAnonymizedValue_LastWins()
    {
        var headers = Headers("Company");
        var rows    = OneRow(("Company", "CUST-aabbcc"));
        var transcode = new List<TranscodeEntry>
        {
            new("Company", "First Corp",  "CUST-aabbcc", "CustomerCompany", 1),
            new("Company", "Second Corp", "CUST-aabbcc", "CustomerCompany", 2),
        };

        var result = Sut.DeAnonymize(headers, rows, transcode);

        Assert.Equal("Second Corp", result.RestoredRows[0]["Company"]);
    }
}

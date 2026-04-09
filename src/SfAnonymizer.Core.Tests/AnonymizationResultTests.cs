using SfAnonymizer.Core.Models;
using Xunit;

namespace SfAnonymizer.Core.Tests;

public class AnonymizationResultTests
{
    private static TranscodeEntry Entry(string column, int row) =>
        new(column, "orig", "anon", "Cat", row);

    // ── TotalReplacements ─────────────────────────────────────────────────────

    [Fact]
    public void TotalReplacements_EmptyTranscodeTable_ReturnsZero()
    {
        var result = new AnonymizationResult
        {
            Headers        = [],
            Rows           = [],
            TranscodeTable = [],
        };

        Assert.Equal(0, result.TotalReplacements);
    }

    [Fact]
    public void TotalReplacements_ReflectsTranscodeTableCount()
    {
        var result = new AnonymizationResult
        {
            Headers        = [],
            Rows           = [],
            TranscodeTable = [Entry("Col", 1), Entry("Col", 2), Entry("Col", 3)],
        };

        Assert.Equal(3, result.TotalReplacements);
    }

    // ── AffectedRows ──────────────────────────────────────────────────────────

    [Fact]
    public void AffectedRows_EmptyTranscodeTable_ReturnsZero()
    {
        var result = new AnonymizationResult { Headers = [], Rows = [], TranscodeTable = [] };
        Assert.Equal(0, result.AffectedRows);
    }

    [Fact]
    public void AffectedRows_CountsDistinctRowIndexes()
    {
        // Three entries but only two distinct row indexes (1 and 2)
        var result = new AnonymizationResult
        {
            Headers        = [],
            Rows           = [],
            TranscodeTable = [Entry("ColA", 1), Entry("ColB", 1), Entry("ColA", 2)],
        };

        Assert.Equal(2, result.AffectedRows);
    }

    // ── AffectedColumns ───────────────────────────────────────────────────────

    [Fact]
    public void AffectedColumns_EmptyTranscodeTable_ReturnsZero()
    {
        var result = new AnonymizationResult { Headers = [], Rows = [], TranscodeTable = [] };
        Assert.Equal(0, result.AffectedColumns);
    }

    [Fact]
    public void AffectedColumns_CountsDistinctColumnNames()
    {
        // Three entries but only two distinct column names (ColA and ColB)
        var result = new AnonymizationResult
        {
            Headers        = [],
            Rows           = [],
            TranscodeTable = [Entry("ColA", 1), Entry("ColB", 1), Entry("ColA", 2)],
        };

        Assert.Equal(2, result.AffectedColumns);
    }
}

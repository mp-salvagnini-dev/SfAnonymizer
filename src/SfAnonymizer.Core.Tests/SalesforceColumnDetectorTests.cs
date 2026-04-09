using SfAnonymizer.Core.Detectors;
using SfAnonymizer.Core.Models;
using Xunit;

namespace SfAnonymizer.Core.Tests;

public class SalesforceColumnDetectorTests
{
    private static readonly SalesforceColumnDetector Sut = new();

    private static List<ColumnClassification> Classify(params string[] headers) =>
        Sut.Classify(headers);

    private static SensitiveDataCategory? CategoryOf(string header) =>
        Classify(header).FirstOrDefault()?.Category;

    // ── CustomerCompany ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("Account.Name")]
    [InlineData("Account Name")]
    [InlineData("AccountName")]
    [InlineData("Company")]
    [InlineData("Customer")]
    [InlineData("Client")]
    [InlineData("Organization Name")]
    [InlineData("Organisation Name")]
    [InlineData("Organisation")]
    [InlineData("Organization")]
    public void Classify_CustomerCompanyColumn_DetectedAsCustomerCompany(string header)
    {
        Assert.Equal(SensitiveDataCategory.CustomerCompany, CategoryOf(header));
    }

    // ── SerialNumber ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Serial Number")]
    [InlineData("SerialNumber")]
    [InlineData("Serial No")]
    [InlineData("Serial#")]
    [InlineData("Asset Serial")]
    [InlineData("SN")]
    public void Classify_SerialNumberColumn_DetectedAsSerialNumber(string header)
    {
        Assert.Equal(SensitiveDataCategory.SerialNumber, CategoryOf(header));
    }

    // ── PersonName ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("First Name")]
    [InlineData("Last Name")]
    [InlineData("Full Name")]
    [InlineData("Contact Name")]
    [InlineData("Name")]
    public void Classify_PersonNameColumn_DetectedAsPersonName(string header)
    {
        Assert.Equal(SensitiveDataCategory.PersonName, CategoryOf(header));
    }

    // ── MachineType ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Machine Type")]
    [InlineData("Machine Family")]
    [InlineData("Machine Model")]
    [InlineData("Product Family")]
    [InlineData("Asset Type")]
    [InlineData("Model")]
    [InlineData("Family")]
    public void Classify_MachineTypeColumn_DetectedAsMachineType(string header)
    {
        Assert.Equal(SensitiveDataCategory.MachineType, CategoryOf(header));
    }

    // ── Non-sensitive columns ─────────────────────────────────────────────────

    [Theory]
    [InlineData("Status")]
    [InlineData("Priority")]
    [InlineData("Description")]
    [InlineData("Ticket ID")]
    [InlineData("Created Date")]
    [InlineData("Category")]
    [InlineData("Subject")]
    [InlineData("Owner")]
    public void Classify_NonSensitiveColumn_NotDetected(string header)
    {
        var classifications = Classify(header);
        Assert.Empty(classifications);
    }

    // ── Case insensitivity ────────────────────────────────────────────────────

    [Theory]
    [InlineData("COMPANY",      SensitiveDataCategory.CustomerCompany)]
    [InlineData("company",      SensitiveDataCategory.CustomerCompany)]
    [InlineData("SERIAL NUMBER",SensitiveDataCategory.SerialNumber)]
    [InlineData("FIRST NAME",   SensitiveDataCategory.PersonName)]
    [InlineData("MACHINE TYPE", SensitiveDataCategory.MachineType)]
    public void Classify_ColumnNameCaseVariants_DetectedCorrectly(string header, SensitiveDataCategory expected)
    {
        Assert.Equal(expected, CategoryOf(header));
    }

    // ── Multiple headers at once ──────────────────────────────────────────────

    [Fact]
    public void Classify_MultipleHeaders_AllSensitiveColumnsDetected()
    {
        var headers = new[] { "Account.Name", "Serial Number", "First Name", "Machine Type", "Status" };

        var result = Classify(headers);

        Assert.Equal(4, result.Count);
        Assert.Contains(result, c => c.ColumnName == "Account.Name"   && c.Category == SensitiveDataCategory.CustomerCompany);
        Assert.Contains(result, c => c.ColumnName == "Serial Number"  && c.Category == SensitiveDataCategory.SerialNumber);
        Assert.Contains(result, c => c.ColumnName == "First Name"     && c.Category == SensitiveDataCategory.PersonName);
        Assert.Contains(result, c => c.ColumnName == "Machine Type"   && c.Category == SensitiveDataCategory.MachineType);
    }

    [Fact]
    public void Classify_NoSensitiveHeaders_ReturnsEmptyList()
    {
        var result = Classify("Status", "Priority", "Description");
        Assert.Empty(result);
    }

    [Fact]
    public void Classify_EmptyHeaderList_ReturnsEmptyList()
    {
        var result = Sut.Classify([]);
        Assert.Empty(result);
    }

    // ── IsAutoDetected flag ───────────────────────────────────────────────────

    [Fact]
    public void Classify_DetectedColumn_HasIsAutoDetectedTrue()
    {
        var result = Classify("Company");
        Assert.True(result[0].IsAutoDetected);
    }

    // ── Partial / ambiguous names that should NOT match ───────────────────────

    [Theory]
    [InlineData("Company Name")]   // "company" only matches ^company$ (standalone)
    [InlineData("First")]          // PersonName requires "first name", not standalone "first"
    [InlineData("Serial")]         // SerialNumber requires "serial number/no/num/#"
    public void Classify_PartialColumnName_NotDetected(string header)
    {
        var classifications = Classify(header);
        Assert.Empty(classifications);
    }
}

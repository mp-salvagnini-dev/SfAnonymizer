using System.Text.RegularExpressions;
using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Core.Detectors;

/// <summary>
/// Detects whether a column contains sensitive data based on its name and sample values.
/// </summary>
public interface ISensitiveColumnDetector
{
    /// <summary>
    /// Classifies columns from headers and optional sample data.
    /// </summary>
    List<ColumnClassification> Classify(
        IReadOnlyList<string> headers,
        IReadOnlyList<Dictionary<string, string>>? sampleRows = null);
}

public sealed partial class SalesforceColumnDetector : ISensitiveColumnDetector
{
    // Column name patterns → category mapping (case-insensitive)
    private static readonly List<(Regex Pattern, SensitiveDataCategory Category)> ColumnNamePatterns =
    [
        // Customer / Company
        (CustomerCompanyRegex(), SensitiveDataCategory.CustomerCompany),

        // Serial Number
        (SerialNumberRegex(), SensitiveDataCategory.SerialNumber),

        // Person Name (first, last, full, contact name)
        (PersonNameRegex(), SensitiveDataCategory.PersonName),

        // Machine Type / Family
        (MachineTypeRegex(), SensitiveDataCategory.MachineType),
    ];

    public List<ColumnClassification> Classify(
        IReadOnlyList<string> headers,
        IReadOnlyList<Dictionary<string, string>>? sampleRows = null)
    {
        var results = new List<ColumnClassification>();

        foreach (var header in headers)
        {
            // Description-like columns: scan content inline for phone, email, serial
            if (DescriptionRegex().IsMatch(header))
            {
                results.Add(new ColumnClassification(
                    header,
                    SensitiveDataCategory.Custom,
                    IsAutoDetected: true,
                    ScanContent: true));
                continue;
            }

            var nameMatch = ColumnNamePatterns
                .FirstOrDefault(p => p.Pattern.IsMatch(header));

            if (nameMatch != default)
            {
                results.Add(new ColumnClassification(header, nameMatch.Category, IsAutoDetected: true));
            }
        }

        return results;
    }

    // Customer / Company:
    // matches: Account.Name, Account Name, Company, Customer, Client, Organisation, Organization
    [GeneratedRegex(@"(?i)(account[._\s]?name|accountname|^company$|^customer$|^client$|organi[sz]ation[._\s]?name|^organi[sz]ation$)")]
    private static partial Regex CustomerCompanyRegex();

    // Serial Number:
    // matches: Serial Number, SerialNumber, Serial No, Serial#, Asset Serial, SN
    [GeneratedRegex(@"(?i)(serial[._\s]?(number|no|num|#)|asset[._\s]?serial|^sn$)")]
    private static partial Regex SerialNumberRegex();

    // Person Name:
    // matches: First Name, Last Name, Full Name, Contact Name, Name (standalone)
    [GeneratedRegex(@"(?i)(^(first|last|full)[._\s]?name$|contact[._\s]?name|^name$)")]
    private static partial Regex PersonNameRegex();

    // Machine Type / Family:
    // matches: Machine Type, Machine Family, Product Family, Model, Asset Type
    [GeneratedRegex(@"(?i)(machine[._\s]?(type|family|model)|product[._\s]?family|asset[._\s]?type|^model$|^family$)")]
    private static partial Regex MachineTypeRegex();

    // Description-like columns: content will be scanned inline for phone, email, serial
    // matches: Description, Notes, Comment(s), Body, Details, Summary, Text
    [GeneratedRegex(@"(?i)(description|notes?|comments?|body|details?|summary|text)")]
    private static partial Regex DescriptionRegex();
}

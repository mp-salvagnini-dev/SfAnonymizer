namespace SfAnonymizer.Core.Models;

/// <summary>
/// Categories of sensitive data detected in Salesforce exports.
/// </summary>
public enum SensitiveDataCategory
{
    CustomerCompany,
    SerialNumber,
    PersonName,
    MachineType,
    Custom,
}

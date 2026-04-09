using System.Security.Cryptography;
using System.Text;
using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Core.Services;

/// <summary>
/// Generates anonymous replacement tokens using a keyed HMAC-SHA256 hash.
/// The same original value always maps to the same token within a session,
/// but tokens change across sessions because the key is regenerated on Reset().
/// </summary>
public sealed class TokenGenerator
{
    private byte[] _key = RandomNumberGenerator.GetBytes(32);

    /// <summary>
    /// Gets an anonymous token for the given original value.
    /// If a custom category is provided its prefix settings take priority.
    /// </summary>
    public string GetToken(string originalValue, SensitiveDataCategory category, CustomCategoryDefinition? custom = null)
    {
        if (string.IsNullOrWhiteSpace(originalValue))
            return originalValue;

        var hash = ComputeHash(originalValue);

        if (custom is not null)
        {
            return custom.UsePrefix && !string.IsNullOrWhiteSpace(custom.Prefix)
                ? $"{custom.Prefix}-{hash}"
                : hash;
        }

        var prefix = category switch
        {
            SensitiveDataCategory.CustomerCompany => "CUST",
            SensitiveDataCategory.SerialNumber    => "SN",
            SensitiveDataCategory.PersonName      => "PERSON",
            SensitiveDataCategory.MachineType     => "MACH",
            SensitiveDataCategory.Custom          => "CUSTOM",
            SensitiveDataCategory.PhoneNumber     => "PHONE",
            SensitiveDataCategory.Email           => "EMAIL",
            _                                     => "ANON"
        };

        return $"{prefix}-{hash}";
    }

    /// <summary>
    /// Resets the session key. All subsequent tokens will differ from previous runs.
    /// </summary>
    public void Reset() => _key = RandomNumberGenerator.GetBytes(32);

    private string ComputeHash(string value)
    {
        var data = Encoding.UTF8.GetBytes(value);
        var hashBytes = HMACSHA256.HashData(_key, data);
        return Convert.ToHexString(hashBytes)[..6];
    }
}

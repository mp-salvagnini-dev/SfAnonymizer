using System.Text.RegularExpressions;
using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Core.Services;

/// <summary>
/// Scans free-text cell content and replaces sensitive patterns (phone, email, serial number) inline.
/// Used for description-like columns where only parts of the text are sensitive.
/// </summary>
public interface IContentScanner
{
    /// <summary>
    /// Replaces all sensitive matches in <paramref name="text"/> with tokens.
    /// Calls <paramref name="getToken"/> for each unique matched value.
    /// Calls <paramref name="onMatch"/> to record each replacement for the transcode table.
    /// </summary>
    string ReplaceMatches(
        string text,
        Func<string, SensitiveDataCategory, string> getToken,
        Action<string, string, SensitiveDataCategory> onMatch);
}

public sealed partial class ContentScanner : IContentScanner
{
    private readonly IItalianNameDetector? _nameDetector;

    public ContentScanner()
    {
        try { _nameDetector = new ItalianNameDetector(); }
        catch { /* name detection is optional — don't break anonymization */ }
    }

    // Priority order: more specific patterns first to avoid partial overlaps.
    private static readonly List<(Regex Pattern, SensitiveDataCategory Category)> Patterns =
    [
        (EmailRegex(),        SensitiveDataCategory.Email),
        (PhoneRegex(),        SensitiveDataCategory.PhoneNumber),
        (SerialInlineRegex(), SensitiveDataCategory.SerialNumber),
    ];

    public string ReplaceMatches(
        string text,
        Func<string, SensitiveDataCategory, string> getToken,
        Action<string, string, SensitiveDataCategory> onMatch)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Collect all matches across all patterns, sorted by position.
        // Skip overlapping matches (first match wins).
        var allMatches = new List<(int Start, int End, string Value, SensitiveDataCategory Category)>();

        foreach (var (pattern, category) in Patterns)
        {
            foreach (Match m in pattern.Matches(text))
            {
                var value = m.Value.Trim();
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (allMatches.Any(existing => m.Index < existing.End && m.Index + m.Length > existing.Start))
                    continue;

                allMatches.Add((m.Index, m.Index + m.Length, value, category));
            }
        }

        // Italian name detection (optional — skipped if detector failed to load)
        if (_nameDetector is not null)
        {
            try
            {
                foreach (var (start, end, value) in _nameDetector.FindNames(text))
                {
                    if (allMatches.Any(existing => start < existing.End && end > existing.Start))
                        continue;

                    allMatches.Add((start, end, value, SensitiveDataCategory.PersonName));
                }
            }
            catch { /* don't let name detection failures break the rest */ }
        }

        if (allMatches.Count == 0)
            return text;

        // Sort by position and build replaced string
        allMatches.Sort((a, b) => a.Start.CompareTo(b.Start));

        var sb = new System.Text.StringBuilder();
        var cursor = 0;

        foreach (var (start, end, value, category) in allMatches)
        {
            sb.Append(text, cursor, start - cursor);

            var token = getToken(value, category);
            onMatch(value, token, category);
            sb.Append(token);

            cursor = end;
        }

        sb.Append(text, cursor, text.Length - cursor);
        return sb.ToString();
    }

    // Email: standard RFC-ish pattern
    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.None)]
    private static partial Regex EmailRegex();

    // Italian phone numbers only — avoids timestamps like "2024-04-15 110859"
    // Mobile: 3xx xxx xxxx (with optional +39/0039 prefix)
    // Toll-free: 800/803 xxx xxx
    // International prefix: +39 or 0039 followed by any valid number
    [GeneratedRegex(@"(?:\+39|0039)[\s\-.]?\d[\s\-.]?\d{3,4}[\s\-.]?\d{3,4}|(?<!\d)3\d{2}[\s\-.]?\d{3,4}[\s\-.]?\d{3,4}(?!\d)|(?<!\d)80[0-9][\s\-.]?\d{3}[\s\-.]?\d{3}(?!\d)", RegexOptions.None)]
    private static partial Regex PhoneRegex();

    // Serial inline: 1–4 capital letters optionally followed by digits, optional underscore, then exactly 4 digits
    // Examples: ABC_1234, XY12_5678, A_0001, ABC1234, XY125678
    [GeneratedRegex(@"\b[A-Z]{1,4}[0-9]*_?[0-9]{4}\b", RegexOptions.None)]
    private static partial Regex SerialInlineRegex();
}

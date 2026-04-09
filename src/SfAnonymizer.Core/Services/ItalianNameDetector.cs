using System.Reflection;
using System.Text.Json;

namespace SfAnonymizer.Core.Services;

/// <summary>
/// Detects Italian personal names (first name + optional adjacent last name) in free text.
/// Loads a curated list of Italian first names from an embedded JSON resource.
/// Last name detection heuristic: an adjacent word that starts with a capital letter,
/// is all-letters, ≥3 chars, and is not a common Italian stopword.
/// </summary>
public interface IItalianNameDetector
{
    List<(int Start, int End, string Value)> FindNames(string text);
}

public sealed class ItalianNameDetector : IItalianNameDetector
{
    private readonly HashSet<string> _names;

    // Common Italian words that appear capitalized (articles, prepositions, sentence starters,
    // titles) which should NOT be treated as last names.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Articles / determiners
        "Il", "Lo", "La", "I", "Gli", "Le", "Un", "Uno", "Una",
        // Prepositions + contractions
        "Di", "A", "Da", "In", "Con", "Su", "Per", "Tra", "Fra",
        "Del", "Dello", "Della", "Dei", "Degli", "Delle",
        "Al", "Allo", "Alla", "Ai", "Agli", "Alle",
        "Dal", "Dallo", "Dalla", "Dai", "Dagli", "Dalle",
        "Nel", "Nello", "Nella", "Nei", "Negli", "Nelle",
        "Sul", "Sullo", "Sulla", "Sui", "Sugli", "Sulle",
        "Col", "Coi",
        // Conjunctions / connectors
        "E", "O", "Ma", "Se", "Che", "Chi", "Come", "Quando", "Dove",
        "Perché", "Quindi", "Però", "Anche", "Oppure", "Né",
        // Common verbs / pronouns that start sentences
        "Sono", "Siamo", "Sei", "Siete", "È", "Ho", "Ha", "Hanno",
        "Lui", "Lei", "Noi", "Voi", "Loro", "Io", "Tu",
        "Si", "Ci", "Ne", "Mi", "Ti", "Vi",
        "Non", "Già", "Qui", "Qua", "Lì", "Là",
        // Demonstratives
        "Questo", "Questa", "Questi", "Queste",
        "Quello", "Quella", "Quelli", "Quelle",
        // Street / location prefixes
        "Via", "Corso", "Piazza", "Largo", "Viale", "Vicolo",
        // Titles / honorifics
        "Sig", "Dott", "Ing", "Avv", "Prof", "Rag", "Geom",
        "Signor", "Signora", "Dottor", "Dottore", "Dottoressa",
        "Ingegnere", "Avvocato", "Professore", "Professoressa",
        // Common ticket words that are capitalized
        "Cliente", "Utente", "Ticket", "Case", "Note", "Oggetto",
        "Problema", "Soluzione", "Stato", "Data", "Ora",
    };

    public ItalianNameDetector()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "SfAnonymizer.Core.Resources.italian_names.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var doc = JsonDocument.Parse(stream);
        var namesArray = doc.RootElement.GetProperty("names");
        _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in namesArray.EnumerateArray())
            _names.Add(el.GetString() ?? string.Empty);
    }

    public List<(int Start, int End, string Value)> FindNames(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var words = TokenizeWords(text);
        var results = new List<(int Start, int End, string Value)>();

        for (var i = 0; i < words.Count; i++)
        {
            var (wStart, wEnd, word) = words[i];
            if (!char.IsUpper(word[0]) || !_names.Contains(word)) continue;

            var matchStart = wStart;
            var matchEnd = wEnd;

            // Check word immediately before: could be a last name
            if (i > 0 && IsLikelyLastName(words[i - 1].Word))
                matchStart = words[i - 1].Start;

            // Check word immediately after: could be a last name
            // (only if we didn't already expand backward, or expand both if both qualify)
            if (i < words.Count - 1 && IsLikelyLastName(words[i + 1].Word))
                matchEnd = words[i + 1].End;

            results.Add((matchStart, matchEnd, text[matchStart..matchEnd]));

            // If we absorbed the word after, skip it so we don't emit it separately
            if (matchEnd > wEnd) i++;
        }

        return results;
    }

    private bool IsLikelyLastName(string word) =>
        word.Length >= 3
        && char.IsUpper(word[0])
        && word.All(char.IsLetter)
        && !StopWords.Contains(word)
        && !_names.Contains(word); // avoid treating another first name as a last name

    private static List<(int Start, int End, string Word)> TokenizeWords(string text)
    {
        var words = new List<(int Start, int End, string Word)>();
        var i = 0;
        while (i < text.Length)
        {
            if (char.IsLetter(text[i]))
            {
                var start = i;
                while (i < text.Length && char.IsLetter(text[i])) i++;
                words.Add((start, i, text[start..i]));
            }
            else
            {
                i++;
            }
        }
        return words;
    }

}

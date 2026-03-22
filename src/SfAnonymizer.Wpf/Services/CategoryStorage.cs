using System.IO;
using System.Text.Json;
using SfAnonymizer.Core.Models;

namespace SfAnonymizer.Wpf.Services;

/// <summary>
/// Persists user-defined custom categories to %AppData%\ITSMTicketAnonymizer\categories.json.
/// </summary>
public static class CategoryStorage
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ITSMTicketAnonymizer",
        "categories.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<CustomCategoryDefinition> Load()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<CustomCategoryDefinition>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<CustomCategoryDefinition> categories)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(categories.ToList(), JsonOptions));
    }
}

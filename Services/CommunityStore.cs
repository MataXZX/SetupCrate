using System.IO;
using System.Text.Json;
using SetupCrate.Models;

namespace SetupCrate.Services;

public static class CommunityStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string Folder(string baseDir) => Path.Combine(baseDir, "community");

    public static IReadOnlyList<CommunityCatalog> LoadAll(string baseDir)
    {
        var result = new List<CommunityCatalog>();
        var folder = Folder(baseDir);

        try
        {
            if (!Directory.Exists(folder)) return result;

            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                try
                {
                    var catalog = JsonSerializer.Deserialize<CommunityCatalog>(File.ReadAllText(file), Options);
                    if (catalog is null || string.IsNullOrWhiteSpace(catalog.Name)) continue;

                    if (string.IsNullOrWhiteSpace(catalog.Id))
                        catalog.Id = Path.GetFileNameWithoutExtension(file);

                    result.Add(catalog);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return result.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static string Save(string baseDir, CommunityCatalog catalog)
    {
        var folder = Folder(baseDir);
        Directory.CreateDirectory(folder);

        var name = SafeName(catalog.Name);
        var path = Path.Combine(folder, name + ".json");

        File.WriteAllText(path, JsonSerializer.Serialize(catalog, Options));
        return path;
    }

    public static void Delete(string baseDir, CommunityCatalog catalog)
    {
        try
        {
            var folder = Folder(baseDir);
            if (!Directory.Exists(folder)) return;

            var targets = new List<string>
            {
                Path.Combine(folder, SafeName(catalog.Name) + ".json"),
                Path.Combine(folder, SafeName(catalog.Id) + ".json")
            };

            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                if (targets.Contains(file, StringComparer.OrdinalIgnoreCase)) continue;

                try
                {
                    var other = JsonSerializer.Deserialize<CommunityCatalog>(File.ReadAllText(file), Options);
                    if (other is null) continue;

                    var sameId = !string.IsNullOrWhiteSpace(catalog.Id) &&
                                 string.Equals(other.Id, catalog.Id, StringComparison.OrdinalIgnoreCase);
                    var sameName = string.Equals(other.Name, catalog.Name, StringComparison.OrdinalIgnoreCase);

                    if (sameId || sameName) targets.Add(file);
                }
                catch
                {
                }
            }

            foreach (var path in targets.Distinct(StringComparer.OrdinalIgnoreCase))
                if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    public static string ToJson(CommunityCatalog catalog) => JsonSerializer.Serialize(catalog, Options);

    public static CommunityCatalog? Parse(string json)
    {
        try
        {
            var catalog = JsonSerializer.Deserialize<CommunityCatalog>(json, Options);
            return catalog is null || string.IsNullOrWhiteSpace(catalog.Name) ? null : catalog;
        }
        catch
        {
            return null;
        }
    }

    public static CommunityCatalog FromCatalog(string name, string author, IEnumerable<CatalogItem> items) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = name.Trim(),
            Author = author.Trim(),
            Version = "1.0",
            Updated = DateTime.Now.ToString("yyyy-MM-dd"),
            Apps = items.Select(item => new CommunityApp
            {
                Name = item.Name,
                Url = item.Url,
                Site = item.Site,
                Notes = item.Notes,
                Category = item.Category
            }).ToList()
        };

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(clean)) clean = "catalog";

        return clean.Length > 80 ? clean[..80] : clean;
    }
}

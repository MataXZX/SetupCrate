using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using SetupCrate.Models;

namespace SetupCrate.Services;

public sealed class CatalogStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string FilePath { get; }

    public CatalogStore(string baseDir)
    {
        FilePath = Path.Combine(baseDir, "catalog.json");
    }

    public CatalogFile Load()
    {
        if (!File.Exists(FilePath)) return new CatalogFile();
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<CatalogFile>(json, Options) ?? new CatalogFile();
        }
        catch (JsonException)
        {
            // Битый файл не теряем: уводим в сторону, чтобы можно было восстановить руками.
            var broken = FilePath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            try { File.Move(FilePath, broken, true); } catch { /* ignore */ }
            return new CatalogFile();
        }
    }

    public void Save(CatalogFile catalog)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(catalog, Options));
        File.Move(tmp, FilePath, true);
    }
}

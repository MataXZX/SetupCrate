using System.IO;
using System.Text.Json;

namespace SetupCrate.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "auto";
    public bool CommunityWarningSeen { get; set; }
    public bool CommunityInstallAccepted { get; set; }
    public bool PublishConsentAccepted { get; set; }
    public List<string> HiddenPackIds { get; set; } = new();
}

public static class SettingsStore
{
    public static string PathFor(string baseDir) => Path.Combine(baseDir, "settings.json");

    public static AppSettings Load(string baseDir)
    {
        try
        {
            var path = PathFor(baseDir);
            if (!File.Exists(path)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(string baseDir, AppSettings settings)
    {
        try
        {
            File.WriteAllText(PathFor(baseDir),
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch {  }
    }
}

using System.Text;
using Microsoft.Win32;

namespace SetupCrate.Services;

public sealed record InstalledApp(string DisplayName, string? Version);

/// <summary>Ищет установленные программы в реестре (Uninstall) и сверяет их с каталогом.</summary>
public static class InstalledScanner
{
    private static readonly string[] UninstallPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public static List<InstalledApp> Scan()
    {
        var result = new List<InstalledApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var path in UninstallPaths)
            {
                try
                {
                    using var root = hive.OpenSubKey(path);
                    if (root == null) continue;

                    foreach (var subName in root.GetSubKeyNames())
                    {
                        using var app = root.OpenSubKey(subName);
                        if (app == null) continue;

                        if (app.GetValue("DisplayName") is not string display) continue;
                        display = display.Trim();
                        if (display.Length == 0) continue;
                        if (display.StartsWith("Update for ", StringComparison.OrdinalIgnoreCase)) continue;
                        if (display.StartsWith("Security Update", StringComparison.OrdinalIgnoreCase)) continue;
                        if (display.StartsWith("KB", StringComparison.OrdinalIgnoreCase)) continue;

                        if (!seen.Add(display)) continue;

                        var version = app.GetValue("DisplayVersion") as string;
                        result.Add(new InstalledApp(display, string.IsNullOrWhiteSpace(version) ? null : version.Trim()));
                    }
                }
                catch
                {
                    // Нет доступа к ветке — пропускаем.
                }
            }
        }

        return result;
    }

    /// <summary>Сравнивает название из каталога с установленным (без учёта регистра, пробелов и знаков).</summary>
    public static bool Matches(string catalogName, string installedName)
    {
        var catalog = Normalize(catalogName);
        var installed = Normalize(installedName);
        if (catalog.Length < 4 || installed.Length < 4) return false;

        return installed.Contains(catalog, StringComparison.Ordinal)
               || catalog.Contains(installed, StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) builder.Append(ch);
        }

        return builder.ToString();
    }
}

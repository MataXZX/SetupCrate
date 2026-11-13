using System.Net.Http;
using System.Text.Json;

namespace SetupCrate.Services;

public sealed record UpdateInfo(string Version, string PageUrl, string? AssetUrl);

public static class UpdateChecker
{
    private const string Repo = "MataXZX/SetupCrate";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SetupCrate");
        return client;
    }

    public static async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        try
        {
            var json = await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest", ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var page = root.GetProperty("html_url").GetString() ?? string.Empty;
            string? asset = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var item in assets.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString() ?? string.Empty;
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                    asset = item.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            return Compare(tag, page, asset, current);
        }
        catch
        {
        }

        try
        {
            using var response = await Http.GetAsync(
                $"https://github.com/{Repo}/releases/latest", HttpCompletionOption.ResponseHeadersRead, ct);

            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;
            var index = finalUrl.IndexOf("/tag/", StringComparison.Ordinal);
            if (index < 0) return null;

            var tag = finalUrl[(index + 5)..].Trim('/');
            return Compare(tag, finalUrl, null, current);
        }
        catch
        {
            return null;
        }
    }

    private static UpdateInfo? Compare(string tag, string page, string? asset, Version current)
    {
        var clean = tag.TrimStart('v', 'V');
        if (!Version.TryParse(clean, out var latest)) return null;

        return latest > current ? new UpdateInfo(clean, page, asset) : null;
    }
}

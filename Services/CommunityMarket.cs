using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SetupCrate.Services;

public sealed class MarketEntry
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public int AppCount { get; set; }
    public int Likes { get; set; }
    public int Dislikes { get; set; }
    public int Downloads { get; set; }
    public List<string> Tags { get; set; } = new();

    public bool Verified { get; set; }

    public string ChannelUrl { get; set; } = "";

    public bool HasChannel => !string.IsNullOrWhiteSpace(ChannelUrl);

    public string TagsText => string.Join(" · ", Tags);
    public string RatingText => $"👍 {Likes}   👎 {Dislikes}";
    public string DownloadsText => $"↓ {Downloads}";
}

public static class CommunityMarket
{
    public static string IndexUrl { get; set; } = "https://setupcrate-store-mataxzx.msk1.amvera.tech/api/index";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SetupCrate");
        return client;
    }

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(IndexUrl);

    public static async Task<IReadOnlyList<MarketEntry>> LoadAsync(string baseDir, CancellationToken ct = default)
    {
        if (!IsConfigured) return LoadLocal(baseDir);

        try
        {
            var json = await Http.GetStringAsync(IndexUrl, ct);
            return Parse(json);
        }
        catch
        {
            return LoadLocal(baseDir);
        }
    }

    public static IReadOnlyList<MarketEntry> LoadLocal(string baseDir)
    {
        try
        {
            var path = Path.Combine(baseDir, "community", "market.json");
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : Array.Empty<MarketEntry>();
        }
        catch
        {
            return Array.Empty<MarketEntry>();
        }
    }

    public static IReadOnlyList<MarketEntry> Parse(string json)
    {
        var result = new List<MarketEntry>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("catalogs", out var catalogs)) return result;

            foreach (var item in catalogs.EnumerateArray())
            {
                var entry = new MarketEntry
                {
                    Id = ReadString(item, "id"),
                    Name = ReadString(item, "name"),
                    Author = ReadString(item, "author"),
                    Description = ReadString(item, "description"),
                    DownloadUrl = ReadString(item, "download"),
                    AppCount = ReadInt(item, "appCount"),
                    Likes = ReadInt(item, "likes"),
                    Dislikes = ReadInt(item, "dislikes"),
                    Downloads = ReadInt(item, "downloads"),
                    Verified = ReadBool(item, "verified"),
                    ChannelUrl = ReadString(item, "channelUrl")
                };

                if (item.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    entry.Tags = tags.EnumerateArray()
                        .Select(t => t.GetString() ?? "")
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(entry.Name)) result.Add(entry);
            }
        }
        catch
        {
        }

        return result;
    }

public static string BaseUrl
    {
        get
        {
            var url = IndexUrl;
            var index = url.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
            return index > 0 ? url[..index] : url.TrimEnd('/');
        }
    }

    public static async Task<(bool Ok, string Message)> PublishAsync(string packJson, CancellationToken ct = default)
    {
        try
        {
            using var content = new StringContent(packJson, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(BaseUrl + "/api/packs", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return response.IsSuccessStatusCode ? (true, body) : (false, body);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<bool> ReportAsync(string packId, string reason, string comment, CancellationToken ct = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { packId, reason, comment });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync(BaseUrl + "/api/reports", content, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
    public static async Task TrackDownloadAsync(string packId, CancellationToken ct = default)
    {
        try
        {
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync($"{BaseUrl}/api/packs/{packId}/download", content, ct);
            await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
        }
    }

    public static async Task<(int Likes, int Dislikes)?> VoteAsync(string packId, int value, CancellationToken ct = default)
    {
        try
        {
            using var content = new StringContent($"{{\"value\":{value}}}", Encoding.UTF8, "application/json");
            using var response = await Http.PostAsync($"{BaseUrl}/api/packs/{packId}/vote", content, ct);
            if (!response.IsSuccessStatusCode) return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement;
            return (root.GetProperty("likes").GetInt32(), root.GetProperty("dislikes").GetInt32());
        }
        catch
        {
            return null;
        }
    }

    public static async Task<string?> DownloadAsync(string url, CancellationToken ct = default)
    {
        try
        {
            return await Http.GetStringAsync(url, ct);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";

    private static bool ReadBool(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) return false;

        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.Number && value.GetInt32() != 0);
    }
    private static int ReadInt(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
}

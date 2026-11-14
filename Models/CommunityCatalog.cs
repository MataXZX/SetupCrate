using System.Text.Json.Serialization;

namespace SetupCrate.Models;

public sealed class CommunityApp
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Site { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Category { get; set; } = "";
}

public sealed class CommunityCatalog
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public string Updated { get; set; } = "";

    public List<string> Tags { get; set; } = new();

    public bool Verified { get; set; }

    public string ChannelUrl { get; set; } = "";

    public List<CommunityApp> Apps { get; set; } = new();

    [JsonIgnore]
    public int Count => Apps.Count;
}

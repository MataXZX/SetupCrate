namespace SetupCrate.Models;

public sealed class CatalogFile
{
    public int Version { get; set; } = 1;

    public string DownloadDir { get; set; } = "downloads";

    public List<string> Categories { get; set; } = new();

    public List<CatalogItem> Items { get; set; } = new();
}

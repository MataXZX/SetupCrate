namespace SetupCrate.Models;

public sealed class CatalogFile
{
    public int Version { get; set; } = 1;

    /// <summary>Папка для скачанных файлов, относительно каталога программы (или абсолютная).</summary>
    public string DownloadDir { get; set; } = "downloads";

    /// <summary>Пользовательский порядок категорий (без «Все» и «Без категории»), включая пустые.</summary>
    public List<string> Categories { get; set; } = new();

    public List<CatalogItem> Items { get; set; } = new();
}

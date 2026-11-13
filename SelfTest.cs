using System.IO;
using System.Text;
using SetupCrate.Models;
using SetupCrate.Services;

namespace SetupCrate;

internal static class SelfTest
{
    public static async Task<int> RunAsync(string baseDir)
    {
        var log = new StringBuilder();
        var failed = 0;

        void Check(string name, bool ok, string detail = "")
        {
            log.AppendLine($"{(ok ? "PASS" : "FAIL")} | {name} | {detail}");
            if (!ok) failed++;
        }

        // 1. Извлечение ссылок со страницы.
        const string html = "<html><a href=\"setup/app-x64.exe\">x</a><a href=\"/dl/thing.msi\">y</a><a href=\"page.html\">z</a></html>";
        var links = LinkExtractor.Extract(html, new Uri("https://example.com/downloads/"));
        Check("LinkExtractor", links.Count == 2 && links.All(l => l.StartsWith("https://example.com/")),
            string.Join(" ; ", links));

        // 2. Подпись файла с встроенной подписью (dotnet.exe, если есть).
        var signedCandidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        if (File.Exists(signedCandidate))
        {
            var signature = SignatureChecker.Check(signedCandidate);
            Check("Signature(signed)", signature.StartsWith("✓"), signature);
        }

        // 3. Подпись неподписанного файла (наш собственный exe).
        var self = Path.Combine(baseDir, "SetupCrate.exe");
        if (File.Exists(self))
        {
            var unsigned = SignatureChecker.Check(self);
            Check("Signature(unsigned)", unsigned.Contains(Loc.T("нет встроенной подписи")), unsigned);
        }

        // 4. Сохранение и чтение каталога.
        var testDir = Path.Combine(baseDir, "selftest");
        Directory.CreateDirectory(testDir);
        var store = new CatalogStore(testDir);
        var catalog = new CatalogFile { DownloadDir = "downloads" };
        catalog.Items.Add(new CatalogItem { Name = "Тест", Url = "https://example.com/" });
        store.Save(catalog);
        var loaded = store.Load();
        Check("Catalog roundtrip", loaded.Items.Count == 1 && loaded.Items[0].Name == "Тест",
            $"записей: {loaded.Items.Count}");

        // 5. Реальная загрузка файла (проверяет сеть, редиректы, запись файла).
        try
        {
            var downloader = new DownloadService();
            var dir = Path.Combine(testDir, "downloads");
            var item = new CatalogItem
            {
                Name = "SelfTest",
                Url = "https://www.7-zip.org/a/7z1900-x64.exe"
            };
            var result = await downloader.DownloadAsync(item, dir, null, CancellationToken.None);
            Check("Download", File.Exists(result.FilePath) && result.Bytes > 500_000,
                $"{result.FileName}, {result.Bytes} байт");
        }
        catch (Exception ex)
        {
            Check("Download", false, ex.Message);
        }

        // 6. Защита от скачивания HTML-страницы вместо установщика.
        try
        {
            var downloader = new DownloadService();
            var dir = Path.Combine(testDir, "downloads-html");
            var item = new CatalogItem { Name = "Html", Url = "https://example.com/" };
            await downloader.DownloadAsync(item, dir, null, CancellationToken.None);
            Check("Html guard", false, "страница скачалась как установщик");
        }
        catch (InvalidOperationException)
        {
            Check("Html guard", true, "страница отклонена");
        }
        catch (Exception ex)
        {
            Check("Html guard", false, ex.Message);
        }

        log.AppendLine(failed == 0 ? "RESULT: OK" : $"RESULT: FAILED ({failed})");
        File.WriteAllText(Path.Combine(baseDir, "selftest.log"), log.ToString());
        return failed == 0 ? 0 : 1;
    }
}

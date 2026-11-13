using System.Net;
using System.Net.Http;
using SetupCrate.Models;

namespace SetupCrate.Services;

public sealed record LinkCheck(bool Ok, long? Size, string Message);

public static class LinkChecker
{
    public static async Task<LinkCheck> CheckAsync(CatalogItem item, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var result = await TryCheckAsync(item, ct).ConfigureAwait(false);
            if (result.Ok || attempt == 2) return result;
            await Task.Delay(400, ct).ConfigureAwait(false);
        }

        return new LinkCheck(false, null, Loc.T("✗ ссылка не открылась"));
    }

    private static async Task<LinkCheck> TryCheckAsync(CatalogItem item, CancellationToken ct)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, item.Url);
            using var resp = await DownloadService.Http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (resp.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.Forbidden)
                return await ProbeWithGetAsync(item, ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return new LinkCheck(false, null, Loc.F("✗ ссылка не отвечает ({0})", (int)resp.StatusCode));

            var size = resp.Content.Headers.ContentLength;
            if (IsWebPage(resp.Content.Headers.ContentType))
                return new LinkCheck(false, size, Loc.T("⚠ ведёт на страницу сайта, а не на файл"));

            if (size is null or <= 0)
            {
                var probe = await ProbeWithGetAsync(item, ct).ConfigureAwait(false);
                return probe with { Message = Loc.T("✓ ссылка работает") };
            }

            return new LinkCheck(true, size, Loc.T("✓ ссылка работает"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            try
            {
                return await ProbeWithGetAsync(item, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return new LinkCheck(false, null, Loc.T("✗ ссылка не открылась"));
            }
        }
    }

    private static async Task<LinkCheck> ProbeWithGetAsync(CatalogItem item, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, item.Url);
        req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
        using var resp = await DownloadService.Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            return new LinkCheck(false, null, Loc.F("✗ ссылка не отвечает ({0})", (int)resp.StatusCode));

        var size = resp.Content.Headers.ContentRange?.Length ?? resp.Content.Headers.ContentLength;
        if (IsWebPage(resp.Content.Headers.ContentType))
            return new LinkCheck(false, size, Loc.T("⚠ ведёт на страницу сайта, а не на файл"));

        return new LinkCheck(true, size, Loc.T("✓ ссылка работает"));
    }

    private static bool IsWebPage(System.Net.Http.Headers.MediaTypeHeaderValue? contentType)
        => contentType?.MediaType is string media &&
           (media.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
            media.Contains("text/plain", StringComparison.OrdinalIgnoreCase) ||
            media.Contains("xhtml", StringComparison.OrdinalIgnoreCase));
}

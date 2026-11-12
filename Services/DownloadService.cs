using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using SetupCrate.Models;

namespace SetupCrate.Services;

public sealed record DownloadResult(string FilePath, string FileName, long Bytes, string? Sha256, bool Resumed);

public sealed class DownloadService
{
    public static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            ConnectTimeout = TimeSpan.FromSeconds(20),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SetupCrate/1.0");
        return client;
    }

    /// <summary>
    /// Скачивает установщик в папку dir. Если рядом уже есть .part-файл от прошлой попытки,
    /// продолжает с того места (HTTP Range), а не качает заново.
    /// </summary>
    public async Task<DownloadResult> DownloadAsync(CatalogItem item, string dir, IProgress<double>? progress, CancellationToken ct)
    {
        if (!Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new InvalidOperationException("Некорректная ссылка: нужен полный http(s)-адрес.");

        Directory.CreateDirectory(dir);

        var resolved = await ResolveAsync(uri, item, ct).ConfigureAwait(false);
        var name = resolved.FileName;
        var target = Path.Combine(dir, name);
        var part = target + ".part";

        var existing = File.Exists(part) ? new FileInfo(part).Length : 0;
        if (existing > 0 && resolved.Length is > 0 && existing >= resolved.Length)
        {
            // .part уже полного размера — просто переименуем.
            File.Move(part, target, true);
            existing = 0;
            return await FinishAsync(item, target, false, ct).ConfigureAwait(false);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, resolved.Uri);
        if (existing > 0) request.Headers.Range = new RangeHeaderValue(existing, null);

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            if (File.Exists(part)) File.Delete(part);
            existing = 0;
            using var retry = new HttpRequestMessage(HttpMethod.Get, resolved.Uri);
            using var retryResponse = await Http.SendAsync(retry, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            retryResponse.EnsureSuccessStatusCode();
            return await SaveAsync(item, retryResponse, target, 0, progress, ct).ConfigureAwait(false);
        }

        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null &&
            (mediaType.Contains("text/html", StringComparison.OrdinalIgnoreCase) ||
             mediaType.Contains("xhtml", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("по ссылке страница сайта, а не установщик — нужна прямая ссылка на файл");
        }

        if (existing > 0 && response.StatusCode != HttpStatusCode.PartialContent)
            existing = 0; // сервер не умеет Range — качаем заново

        if (existing == 0)
        {
            var better = NameFromUri(response.RequestMessage?.RequestUri);
            if (better != null && !string.Equals(better, name, StringComparison.OrdinalIgnoreCase))
            {
                target = Path.Combine(dir, better);
                part = target + ".part";
                if (File.Exists(part)) File.Delete(part);
            }
        }

        return await SaveAsync(item, response, target, existing, progress, ct).ConfigureAwait(false);
    }

    private async Task<DownloadResult> SaveAsync(CatalogItem item, HttpResponseMessage response, string target,
        long existing, IProgress<double>? progress, CancellationToken ct)
    {
        var part = target + ".part";
        var total = response.Content.Headers.ContentLength is long len ? len + existing : (long?)null;

        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var dest = new FileStream(part, existing > 0 ? FileMode.Append : FileMode.Create,
                         FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
        {
            var buffer = new byte[1 << 16];
            var read = existing;
            int n;
            while ((n = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total is > 0) progress?.Report(Math.Clamp(read / (double)total.Value, 0, 1));
            }

            await dest.FlushAsync(ct).ConfigureAwait(false);
        }

        File.Move(part, target, true);
        return await FinishAsync(item, target, existing > 0, ct).ConfigureAwait(false);
    }

    private static async Task<DownloadResult> FinishAsync(CatalogItem item, string target, bool resumed, CancellationToken ct)
    {
        string? hash = null;
        if (!string.IsNullOrWhiteSpace(item.Sha256))
        {
            await using var fs = File.OpenRead(target);
            hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false));
        }

        return new DownloadResult(target, Path.GetFileName(target), new FileInfo(target).Length, hash, resumed);
    }

    private sealed record Resolved(Uri Uri, string FileName, long? Length);

    private static async Task<Resolved> ResolveAsync(Uri uri, CatalogItem item, CancellationToken ct)
    {
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await Http.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var finalUri = response.RequestMessage?.RequestUri ?? uri;
                return new Resolved(finalUri, NameFromResponse(response, item, finalUri),
                    response.Content.Headers.ContentLength);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // HEAD не поддерживается или запрещён — пойдём обычным GET.
        }

        return new Resolved(uri, NameFromUri(uri) ?? Sanitize(item.Name) + ".exe", null);
    }

    private static string NameFromResponse(HttpResponseMessage response, CatalogItem item, Uri finalUri)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        var fromHeader = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"');
        if (!string.IsNullOrWhiteSpace(fromHeader) && HasInstallerExt(fromHeader)) return Sanitize(fromHeader);

        return NameFromUri(finalUri) ?? Sanitize(item.Name) + ".exe";
    }

    private static string? NameFromUri(Uri? uri)
    {
        if (uri == null) return null;
        var name = Path.GetFileName(uri.AbsolutePath);
        return !string.IsNullOrWhiteSpace(name) && HasInstallerExt(name) ? Sanitize(name) : null;
    }

    private static bool HasInstallerExt(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext is ".exe" or ".msi" or ".msix" or ".zip" or ".7z" or ".appx";
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim();
        return name.Length == 0 ? "installer.exe" : name;
    }
}

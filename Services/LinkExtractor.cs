using System.Net;
using System.Text.RegularExpressions;

namespace SetupCrate.Services;

public static class LinkExtractor
{
    private static readonly Regex DownloadLinkRegex = new(
        "(?:href|src)\\s*=\\s*[\"']([^\"']+?\\.(?:exe|msi|msix|zip|7z|appx))(?:\\?[^\"']*)?[\"']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<string> Extract(string html, Uri baseUri)
    {
        var result = new List<string>();
        foreach (Match match in DownloadLinkRegex.Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups[1].Value);
            if (!Uri.TryCreate(baseUri, href, out var absolute)) continue;
            if (absolute.Scheme != Uri.UriSchemeHttps && absolute.Scheme != Uri.UriSchemeHttp) continue;

            var text = absolute.ToString();
            if (!result.Contains(text)) result.Add(text);
        }

        return result;
    }
}

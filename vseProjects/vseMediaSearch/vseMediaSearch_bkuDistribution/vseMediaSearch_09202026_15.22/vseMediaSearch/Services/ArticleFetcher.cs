using AngleSharp;
using AngleSharp.Dom;

namespace vseMediaSearch.Services;

/// <summary>
/// Fetches and parses HTML article pages: extracts clean, readable text (stripping nav/ads/scripts)
/// and can follow a "next page" link across multiple pages of a paginated article.
/// </summary>
internal static class ArticleFetcher
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (compatible; vseMediaSearch/0.1; +https://github.com/) MCP-Agent");
        return client;
    }

    /// <summary>One extracted page of an article.</summary>
    public sealed record ArticlePage(string Url, string Title, string Text, IReadOnlyList<string> CodeBlocks);

    /// <summary>
    /// Fetches a single page and extracts its main readable text and title.
    /// </summary>
    public static async Task<ArticlePage> FetchPageAsync(string url, CancellationToken cancellationToken = default)
    {
        await SiteRateLimiter.WaitTurnAsync(url, cancellationToken).ConfigureAwait(false);

        var config = Configuration.Default;
        using var context = BrowsingContext.New(config);

        var html = await Http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        var document = await context.OpenAsync(req => req.Content(html).Address(url), cancellationToken)
            .ConfigureAwait(false);

        var codeBlocks = ExtractCodeBlocks(document);
        RemoveNoiseElements(document);

        var title = document.Title?.Trim() ?? string.Empty;
        var text = ExtractReadableText(document);

        return new ArticlePage(url, title, text, codeBlocks);
    }

    /// <summary>
    /// Fetches an article and, if it is paginated, follows "next page" links up to
    /// <paramref name="maxPages"/> pages, concatenating the extracted text in order.
    /// </summary>
    public static async Task<List<ArticlePage>> FetchArticleWithPaginationAsync(
        string startUrl,
        int maxPages = 10,
        CancellationToken cancellationToken = default)
    {
        var pages = new List<ArticlePage>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentUrl = startUrl;

        // Bounded loop (Rule 21A): capped by maxPages and by a visited-set guard against link cycles.
        for (var i = 0; i < maxPages && currentUrl is not null && visited.Add(currentUrl); i++)
        {
            await SiteRateLimiter.WaitTurnAsync(currentUrl, cancellationToken).ConfigureAwait(false);

            var config = Configuration.Default;
            using var context = BrowsingContext.New(config);

            var html = await Http.GetStringAsync(currentUrl, cancellationToken).ConfigureAwait(false);
            var document = await context.OpenAsync(req => req.Content(html).Address(currentUrl), cancellationToken)
                .ConfigureAwait(false);

            var nextUrl = FindNextPageUrl(document, currentUrl);
            var codeBlocks = ExtractCodeBlocks(document);

            RemoveNoiseElements(document);
            var title = document.Title?.Trim() ?? string.Empty;
            var text = ExtractReadableText(document);
            pages.Add(new ArticlePage(currentUrl, title, text, codeBlocks));

            currentUrl = nextUrl;
        }

        return pages;
    }

    internal static void RemoveNoiseElements(IDocument document)
    {
        var selectors = new[] { "script", "style", "nav", "footer", "header", "aside", "noscript", "form", "iframe" };
        foreach (var selector in selectors)
        {
            foreach (var element in document.QuerySelectorAll(selector).ToArray())
            {
                element.Remove();
            }
        }
    }

    /// <summary>
    /// Extracts the text content of every &lt;pre&gt;/&lt;code&gt; block on the page, so code
    /// samples can be surfaced separately from prose text (useful for coding-focused learning).
    /// Must be called BEFORE <see cref="RemoveNoiseElements"/>, since noise elements are removed
    /// destructively from the live document.
    /// </summary>
    internal static IReadOnlyList<string> ExtractCodeBlocks(IDocument document)
    {
        var blocks = new List<string>();
        foreach (var element in document.QuerySelectorAll("pre, code"))
        {
            // Skip <code> elements nested inside a <pre> we've already captured, to avoid duplicates.
            if (element.TagName.Equals("code", StringComparison.OrdinalIgnoreCase) &&
                element.Closest("pre") is not null)
            {
                continue;
            }

            var text = element.TextContent?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                blocks.Add(text);
            }
        }

        return blocks;
    }

    internal static string ExtractReadableText(IDocument document)
    {
        // Prefer common article/content containers if present; otherwise fall back to <body>.
        var contentSelectors = new[] { "article", "main", "[role=main]", "#content", ".content", ".article-body", ".post-content" };
        IElement? contentRoot = null;
        foreach (var selector in contentSelectors)
        {
            contentRoot = document.QuerySelector(selector);
            if (contentRoot is not null)
            {
                break;
            }
        }

        contentRoot ??= document.Body;
        if (contentRoot is null)
        {
            return string.Empty;
        }

        var text = contentRoot.TextContent;
        // Collapse excessive whitespace so extracted text is compact and readable.
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0);
        return string.Join("\n", lines);
    }

    private static string? FindNextPageUrl(IDocument document, string currentUrl)
    {
        // Common patterns for "next page" links across articles/blogs.
        var candidates = document.QuerySelectorAll("a[rel=next], a.next, a[aria-label='Next'], link[rel=next]");
        foreach (var candidate in candidates)
        {
            var href = candidate.GetAttribute("href");
            if (!string.IsNullOrWhiteSpace(href))
            {
                return ResolveUrl(currentUrl, href);
            }
        }

        // Fallback: an anchor whose visible text is exactly "Next" or "Next page".
        foreach (var anchor in document.QuerySelectorAll("a"))
        {
            var text = anchor.TextContent?.Trim();
            if (string.Equals(text, "Next", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "Next page", StringComparison.OrdinalIgnoreCase))
            {
                var href = anchor.GetAttribute("href");
                if (!string.IsNullOrWhiteSpace(href))
                {
                    return ResolveUrl(currentUrl, href);
                }
            }
        }

        return null;
    }

    private static string? ResolveUrl(string baseUrl, string href)
    {
        try
        {
            return new Uri(new Uri(baseUrl), href).ToString();
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}

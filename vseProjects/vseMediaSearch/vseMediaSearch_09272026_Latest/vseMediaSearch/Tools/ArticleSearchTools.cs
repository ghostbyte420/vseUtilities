using ModelContextProtocol.Server;
using System.ComponentModel;
using vseMediaSearch.Services;

namespace vseMediaSearch.Tools;

/// <summary>
/// Tools for searching and reading text-based articles (including multi-page/paginated
/// articles) from an internal, user-controlled list of allowed sites (e.g. CNN.com,
/// w3schools.com, Wikipedia). Results are cached so the same page is never re-fetched.
/// </summary>
internal class ArticleSearchTools
{
    [McpServerTool]
    [Description("Lists the internal list of allowed media/website sources (domains) that article tools may fetch from.")]
    public static List<string> ListAllowedSites()
    {
        return SiteListService.LoadSites();
    }

    [McpServerTool]
    [Description("Adds a domain (e.g. 'cnn.com') to the internal list of allowed sources.")]
    public static List<string> AddAllowedSite(
        [Description("Domain or full URL to allow, e.g. 'cnn.com' or 'https://www.cnn.com'.")] string domain)
    {
        return SiteListService.AddSite(domain);
    }

    [McpServerTool]
    [Description("Removes a domain from the internal list of allowed sources.")]
    public static List<string> RemoveAllowedSite(
        [Description("Domain to remove, e.g. 'cnn.com'.")] string domain)
    {
        return SiteListService.RemoveSite(domain);
    }

    [McpServerTool]
    [Description("Fetches a text article page (or, if paginated, follows 'next page' links up to maxPages) " +
                  "from an allowed site and returns its extracted, readable text. Caches the result so the " +
                  "same URL is not re-fetched on a later call.")]
    public static async Task<string> FetchArticle(
        [Description("Full URL of the article's first page.")] string url,
        [Description("Maximum number of paginated pages to follow (default 10).")] int maxPages = 10)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"'{url}' is not a valid absolute URL.";
        }

        if (!SiteListService.IsAllowed(uri))
        {
            return $"'{uri.Host}' is not in the allowed site list. Use AddAllowedSite to allow it first.";
        }

        var cacheKey = "article_" + uri.AbsoluteUri;
        var cached = KnowledgeCacheService.TryLoad(cacheKey);
        if (cached is not null)
        {
            return CitationFormatter.AppendCitation(cached.Content, cached.SourceUrl, cached.Title, cached.CachedAtUtc);
        }

        var pages = await ArticleFetcher.FetchArticleWithPaginationAsync(url, maxPages).ConfigureAwait(false);
        if (pages.Count == 0)
        {
            return $"No readable content found at {url}.";
        }

        var combinedText = string.Join(
            "\n\n----- (next page) -----\n\n",
            pages.Select(p => p.Text));

        var allCodeBlocks = pages.SelectMany(p => p.CodeBlocks).ToList();
        var fetchedAtUtc = DateTimeOffset.UtcNow;

        KnowledgeCacheService.Save(cacheKey, new KnowledgeCacheService.CachedEntry(
            SourceUrl: url,
            Title: pages[0].Title,
            Content: combinedText,
            CachedAtUtc: fetchedAtUtc,
            CodeBlocks: allCodeBlocks));

        return CitationFormatter.AppendCitation(combinedText, url, pages[0].Title, fetchedAtUtc);
    }

    [McpServerTool]
    [Description("Returns just the code samples (from <pre>/<code> blocks) found in a previously fetched " +
                  "article, without the surrounding prose. Call FetchArticle first if the article has not " +
                  "been cached yet.")]
    public static string GetArticleCodeBlocks(
        [Description("Full URL of the article's first page (same URL used with FetchArticle).")] string url)
    {
        var cacheKey = "article_" + url;
        var cached = KnowledgeCacheService.TryLoad(cacheKey);
        if (cached is null)
        {
            return $"No cached article found for {url}. Call FetchArticle first.";
        }

        if (cached.CodeBlocks is null || cached.CodeBlocks.Count == 0)
        {
            return "No code blocks were found in this article.";
        }

        return string.Join("\n\n----- (next code block) -----\n\n", cached.CodeBlocks);
    }
}

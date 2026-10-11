using ModelContextProtocol.Server;
using System.ComponentModel;
using vseMediaSearch.Services;

namespace vseMediaSearch.Tools;

/// <summary>
/// Tools for searching across everything already learned (cached articles, video
/// transcripts, and on-screen text) in previous sessions, without needing to remember
/// or re-fetch the original URL. This is what makes learning compound over time instead
/// of being a single-use lookup.
/// </summary>
internal class KnowledgeSearchTools
{
    [McpServerTool]
    [Description("Searches every previously learned article/video (cached locally) for a keyword or phrase, " +
                  "and returns matching sources with a short snippet of surrounding context. Use this before " +
                  "re-fetching a URL, to check whether the answer is already known from earlier sessions.")]
    public static string SearchKnowledge(
        [Description("Keyword or phrase to search for (case-insensitive).")] string query,
        [Description("Maximum number of matching sources to return (default 10).")] int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Please provide a non-empty search query.";
        }

        var allEntries = KnowledgeCacheService.LoadAll();
        var matches = new List<string>();

        foreach (var (_, entry) in allEntries)
        {
            if (matches.Count >= maxResults)
            {
                break;
            }

            var snippet = FindSnippet(entry.Content, query)
                ?? FindSnippet(entry.OnScreenText, query)
                ?? FindSnippetInCodeBlocks(entry.CodeBlocks, query);

            if (snippet is null)
            {
                continue;
            }

            var staleNote = KnowledgeCacheService.IsStale(entry) ? " [cached copy is over 30 days old]" : string.Empty;
            matches.Add($"- {entry.Title} ({entry.SourceUrl}), cached {entry.CachedAtUtc:yyyy-MM-dd}{staleNote}:\n  \"{snippet}\"");
        }

        if (matches.Count == 0)
        {
            return $"No previously learned content matched '{query}'. Try FetchArticle or GetYouTubeTranscript to learn something new first.";
        }

        return string.Join("\n\n", matches);
    }

    [McpServerTool]
    [Description("Produces a short local summary of a previously cached article/video's content, " +
                  "without calling any external AI service (uses a simple word-frequency heuristic). " +
                  "Call FetchArticle or GetYouTubeTranscript first if the source has not been cached yet.")]
    public static string SummarizeKnowledge(
        [Description("Full source URL used when the article/video was originally fetched.")] string url,
        [Description("Cache key prefix used by the fetch tool: 'article' for FetchArticle URLs, 'youtube' for YouTube videos.")] string sourceType = "article",
        [Description("Maximum number of sentences to include in the summary (default 5).")] int maxSentences = 5)
    {
        var cacheKey = sourceType.Trim().ToLowerInvariant() switch
        {
            "youtube" or "video" => "youtube_" + url,
            _ => "article_" + url,
        };

        var cached = KnowledgeCacheService.TryLoad(cacheKey);
        if (cached is null)
        {
            return $"No cached content found for {url}. Fetch it first with FetchArticle or GetYouTubeTranscript.";
        }

        if (string.IsNullOrWhiteSpace(cached.Content))
        {
            return "The cached entry has no text content to summarize.";
        }

        return SummarizationService.Summarize(cached.Content, maxSentences);
    }

    private static string? FindSnippet(string? content, string query)
    {
        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        var index = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        const int contextChars = 80;
        var start = Math.Max(0, index - contextChars);
        var end = Math.Min(content.Length, index + query.Length + contextChars);
        var snippet = content[start..end].Replace('\n', ' ').Trim();

        return (start > 0 ? "..." : string.Empty) + snippet + (end < content.Length ? "..." : string.Empty);
    }

    private static string? FindSnippetInCodeBlocks(IReadOnlyList<string>? codeBlocks, string query)
    {
        if (codeBlocks is null)
        {
            return null;
        }

        foreach (var block in codeBlocks)
        {
            var snippet = FindSnippet(block, query);
            if (snippet is not null)
            {
                return snippet;
            }
        }

        return null;
    }
}

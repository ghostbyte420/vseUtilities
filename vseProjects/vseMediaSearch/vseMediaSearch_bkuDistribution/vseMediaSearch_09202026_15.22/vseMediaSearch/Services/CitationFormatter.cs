namespace vseMediaSearch.Services;

/// <summary>
/// Formats a simple, consistent "Source: ..." citation footer appended to tool output, so a
/// reader (or a downstream AI) can always tell exactly where information came from and when
/// it was fetched — important for beginners who may not otherwise think to double check.
/// </summary>
internal static class CitationFormatter
{
    public static string AppendCitation(string content, string sourceUrl, string title, DateTimeOffset fetchedAtUtc)
    {
        var titlePart = string.IsNullOrWhiteSpace(title) ? string.Empty : $"\"{title}\" — ";
        var footer = $"\n\n---\nSource: {titlePart}{sourceUrl}\nFetched: {fetchedAtUtc:yyyy-MM-dd HH:mm} UTC";
        return content + footer;
    }
}

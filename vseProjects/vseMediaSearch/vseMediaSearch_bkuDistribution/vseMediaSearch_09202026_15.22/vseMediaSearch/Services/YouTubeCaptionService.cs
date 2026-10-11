using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace vseMediaSearch.Services;

/// <summary>
/// Fetches YouTube's own caption/transcript track (when the video has one) via YouTube's
/// public timedtext endpoint. This is the fast, free path: no audio download, no local
/// transcription model needed. Falls back to <see cref="WhisperTranscriptionService"/> when
/// no caption track is available.
/// </summary>
internal static partial class YouTubeCaptionService
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        // Without a browser-like User-Agent, YouTube sometimes serves a stripped-down page
        // that omits the embedded captionTracks JSON, forcing an unnecessary (and much
        // slower) fallback to audio download + local Whisper transcription.
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        return client;
    }

    /// <summary>One caption line with its start time.</summary>
    public sealed record CaptionLine(TimeSpan Start, string Text);

    /// <summary>
    /// Attempts to fetch the caption track for a YouTube video. Returns null if no
    /// caption/transcript track exists for the video (caller should fall back to Whisper).
    /// </summary>
    public static async Task<List<CaptionLine>?> TryFetchCaptionsAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var watchUrl = $"https://www.youtube.com/watch?v={videoId}";
        string watchHtml;
        try
        {
            watchHtml = await Http.GetStringAsync(watchUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var captionTrackUrl = ExtractCaptionTrackUrl(watchHtml);
        if (captionTrackUrl is null)
        {
            return null;
        }

        string xml;
        try
        {
            xml = await Http.GetStringAsync(captionTrackUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        return ParseTimedText(xml);
    }

    private static string? ExtractCaptionTrackUrl(string watchHtml)
    {
        // The watch page embeds a JSON blob containing "captionTracks":[{"baseUrl":"..."}]
        var match = CaptionTracksRegex().Match(watchHtml);
        if (!match.Success)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse("[" + match.Groups[1].Value + "]");
            foreach (var track in doc.RootElement.EnumerateArray())
            {
                if (track.TryGetProperty("baseUrl", out var baseUrlProp))
                {
                    var url = baseUrlProp.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        // Unescape JSON-escaped unicode sequences (e.g. \u0026 -> &).
                        return Regex.Unescape(url);
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    internal static List<CaptionLine> ParseTimedText(string xml)
    {
        var lines = new List<CaptionLine>();
        foreach (Match m in TextNodeRegex().Matches(xml))
        {
            var startSeconds = double.Parse(m.Groups["start"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var text = WebUtilityDecode(m.Groups["text"].Value);
            lines.Add(new CaptionLine(TimeSpan.FromSeconds(startSeconds), text));
        }
        return lines;
    }

    private static string WebUtilityDecode(string text) => System.Net.WebUtility.HtmlDecode(text).Trim();

    /// <summary>Formats caption lines into a single timestamped transcript block of text.</summary>
    public static string FormatTranscript(List<CaptionLine> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.Append('[').Append(line.Start.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)).Append("] ").AppendLine(line.Text);
        }
        return sb.ToString();
    }

    [GeneratedRegex("\"captionTracks\":\\[(.*?)\\]")]
    private static partial Regex CaptionTracksRegex();

    [GeneratedRegex("<text start=\"(?<start>[0-9.]+)\"[^>]*>(?<text>.*?)</text>", RegexOptions.Singleline)]
    private static partial Regex TextNodeRegex();
}

using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.RegularExpressions;
using vseMediaSearch.Services;

namespace vseMediaSearch.Tools;

/// <summary>
/// Tools for "watching" and learning from video content (currently YouTube). Tries YouTube's
/// own caption/transcript track first (fast, free, no download); if none exists, falls back
/// to downloading just the audio (via the user-installed yt-dlp tool) into a session folder
/// under the OS's real Downloads folder, then runs it through a local, offline Whisper model
/// for real speech-to-text. Transcripts are cached so a video is never re-processed twice.
/// </summary>
internal partial class VideoLearningTools
{
    private static readonly string SessionId = "session_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);

    [McpServerTool]
    [Description("Fetches a learnable, timestamped transcript for a YouTube video: uses the video's own " +
                  "captions if available, otherwise downloads audio (via yt-dlp, into a session folder under " +
                  "your OS Downloads folder that you control) and runs local Whisper speech-to-text on it. " +
                  "Results are cached so the same video is never re-processed.")]
    public static async Task<string> GetYouTubeTranscript(
        [Description("YouTube video URL or bare video ID.")] string videoUrlOrId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var videoId = ExtractVideoId(videoUrlOrId);
        if (videoId is null)
        {
            return $"Could not extract a YouTube video ID from '{videoUrlOrId}'.";
        }

        var cacheKey = "youtube_" + videoId;
        var cached = KnowledgeCacheService.TryLoad(cacheKey);
        if (cached is not null && !string.IsNullOrWhiteSpace(cached.Content))
        {
            return CitationFormatter.AppendCitation(cached.Content, cached.SourceUrl, cached.Title, cached.CachedAtUtc);
        }

        var captions = await YouTubeCaptionService.TryFetchCaptionsAsync(videoId, cancellationToken).ConfigureAwait(false);
        captions?.RemoveAll(line => string.IsNullOrWhiteSpace(line.Text));
        if (captions is not null && captions.Count > 0)
        {
            var transcript = YouTubeCaptionService.FormatTranscript(captions);
            var captionFetchedAtUtc = DateTimeOffset.UtcNow;
            var captionSourceUrl = $"https://www.youtube.com/watch?v={videoId}";
            cancellationToken.ThrowIfCancellationRequested();
            KnowledgeCacheService.Save(cacheKey, new KnowledgeCacheService.CachedEntry(
                SourceUrl: captionSourceUrl,
                Title: videoId,
                Content: transcript,
                CachedAtUtc: captionFetchedAtUtc));
            return CitationFormatter.AppendCitation(transcript, captionSourceUrl, videoId, captionFetchedAtUtc);
        }

        // No captions available: fall back to real audio transcription.
        string audioPath;
        try
        {
            var sessionFolder = DownloadFolderResolver.GetOrCreateSessionFolder(SessionId);
            audioPath = await YtDlpService.DownloadAudioAsync(
                $"https://www.youtube.com/watch?v={videoId}", sessionFolder, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return $"No captions were available for this video, and audio download failed: {ex.Message}";
        }

        var segments = await WhisperTranscriptionService.TranscribeAsync(audioPath, cancellationToken).ConfigureAwait(false);
        segments.RemoveAll(segment => string.IsNullOrWhiteSpace(segment.Text));
        if (segments.Count == 0)
        {
            return "No speech was transcribed; no knowledge was saved.";
        }
        var whisperTranscript = WhisperTranscriptionService.FormatTranscript(segments);
        var whisperFetchedAtUtc = DateTimeOffset.UtcNow;
        var whisperSourceUrl = $"https://www.youtube.com/watch?v={videoId}";

        cancellationToken.ThrowIfCancellationRequested();
        KnowledgeCacheService.Save(cacheKey, new KnowledgeCacheService.CachedEntry(
            SourceUrl: whisperSourceUrl,
            Title: videoId,
            Content: whisperTranscript,
            CachedAtUtc: whisperFetchedAtUtc));

        return CitationFormatter.AppendCitation(whisperTranscript, whisperSourceUrl, videoId, whisperFetchedAtUtc);
    }

    [McpServerTool]
    [Description("Extracts on-screen text (code, slides, diagrams) shown visually in a YouTube video, " +
                  "using local OCR over sampled video frames \u2014 this catches information shown but never " +
                  "spoken aloud, complementing GetYouTubeTranscript. Downloads the video (via yt-dlp, into a " +
                  "session folder under your OS Downloads folder) and extracts a frame every few seconds " +
                  "(via auto-downloaded ffmpeg) before running Tesseract OCR on each frame. Slower than " +
                  "transcript-only retrieval; results are cached so the same video is never re-processed.")]
    public static async Task<string> GetVideoOnScreenText(
        [Description("YouTube video URL or bare video ID.")] string videoUrlOrId,
        [Description("Seconds between sampled frames (default 5). Ignored if useSceneChangeDetection is true.")] double frameIntervalSeconds = 5.0,
        [Description("If true, samples frames only when the video's visual content changes significantly " +
                      "(e.g. a slide/code change), instead of a fixed time interval. Produces fewer, more " +
                      "meaningful frames for mostly-static content like tutorials.")] bool useSceneChangeDetection = false)
    {
        var videoId = ExtractVideoId(videoUrlOrId);
        if (videoId is null)
        {
            return $"Could not extract a YouTube video ID from '{videoUrlOrId}'.";
        }

        var cacheKey = "youtube_" + videoId;
        var cached = KnowledgeCacheService.TryLoad(cacheKey);
        if (cached?.OnScreenText is not null)
        {
            return cached.OnScreenText;
        }

        var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
        string videoPath;
        try
        {
            var sessionFolder = DownloadFolderResolver.GetOrCreateSessionFolder(SessionId);
            videoPath = await YtDlpService.DownloadVideoAsync(videoUrl, sessionFolder).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return $"Could not download the video for OCR: {ex.Message}";
        }

        var framesFolder = Path.Combine(Path.GetDirectoryName(videoPath) ?? Path.GetTempPath(), videoId + "_frames");
        List<string> framePaths;
        try
        {
            var samplingMode = useSceneChangeDetection ? FrameSamplingMode.SceneChange : FrameSamplingMode.FixedInterval;
            framePaths = await FfmpegService.ExtractFramesAsync(videoPath, framesFolder, frameIntervalSeconds, samplingMode).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return $"Video downloaded, but frame extraction failed: {ex.Message}";
        }

        if (framePaths.Count == 0)
        {
            return "No frames could be extracted from this video.";
        }

        List<string> perFrameText;
        try
        {
            perFrameText = await OcrService.RecognizeFramesAsync(framePaths).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return $"Frames extracted, but OCR failed: {ex.Message}";
        }

        var onScreenText = OcrService.AggregateAndDeduplicate(perFrameText);
        if (onScreenText.Length == 0)
        {
            onScreenText = "No on-screen text was detected in this video's sampled frames.";
        }

        KnowledgeCacheService.Save(cacheKey, cached is null
            ? new KnowledgeCacheService.CachedEntry(
                SourceUrl: videoUrl,
                Title: videoId,
                Content: string.Empty,
                CachedAtUtc: DateTimeOffset.UtcNow,
                OnScreenText: onScreenText)
            : cached with { OnScreenText = onScreenText, CachedAtUtc = DateTimeOffset.UtcNow });

        return onScreenText;
    }

    [McpServerTool]
    [Description("Fetches a transcript for a non-YouTube video URL supported by yt-dlp (e.g. Vimeo, " +
                  "Twitch VODs, many news/media sites). Unlike GetYouTubeTranscript, this always downloads " +
                  "audio (via yt-dlp, into a session folder under your OS Downloads folder) and runs local, " +
                  "offline Whisper speech-to-text, since non-YouTube sites don't expose a caption API this " +
                  "tool can query. Results are cached so the same URL is not re-processed on a later call.")]
    public static async Task<string> GetVideoTranscript(
        [Description("Full URL of the video (any site supported by yt-dlp).")] string videoUrl)
    {
        if (!Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri))
        {
            return $"'{videoUrl}' is not a valid absolute URL.";
        }

        var cacheKey = "video_" + uri.AbsoluteUri;
        var cached = KnowledgeCacheService.TryLoad(cacheKey);
        if (cached is not null)
        {
            return CitationFormatter.AppendCitation(cached.Content, cached.SourceUrl, cached.Title, cached.CachedAtUtc);
        }

        string audioPath;
        try
        {
            var sessionFolder = DownloadFolderResolver.GetOrCreateSessionFolder(SessionId);
            audioPath = await YtDlpService.DownloadAudioAsync(videoUrl, sessionFolder).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return $"Could not download audio for this video: {ex.Message}";
        }

        var segments = await WhisperTranscriptionService.TranscribeAsync(audioPath).ConfigureAwait(false);
        var transcript = WhisperTranscriptionService.FormatTranscript(segments);
        var fetchedAtUtc = DateTimeOffset.UtcNow;
        var title = Path.GetFileNameWithoutExtension(audioPath);

        KnowledgeCacheService.Save(cacheKey, new KnowledgeCacheService.CachedEntry(
            SourceUrl: videoUrl,
            Title: title,
            Content: transcript,
            CachedAtUtc: fetchedAtUtc));

        return CitationFormatter.AppendCitation(transcript, videoUrl, title, fetchedAtUtc);
    }

    [McpServerTool]
    [Description("Searches a free, no-API-key media platform (currently: youtube, soundcloud) for content " +
                  "matching a natural-language query or keywords (e.g. 'fried egg recipe') using yt-dlp's " +
                  "built-in search (no video/track is downloaded). Returns a numbered list of matching " +
                  "titles and URLs; pass one of the returned URLs/IDs into GetYouTubeTranscript or " +
                  "GetVideoTranscript to read/learn its content. Search has a 10-second server-side deadline.")]
    public static async Task<string> SearchMedia(
        [Description("Search text/keywords/sentence describing the content you're looking for.")] string query,
        [Description("Which free platform to search: 'youtube' (default) or 'soundcloud'.")] string platform = "youtube",
        [Description("Maximum number of results to return (default 5).")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        DiagnosticLog.Write("search.enter", $"request={requestId}");
        if (string.IsNullOrWhiteSpace(query))
        {
            DiagnosticLog.Write("search.invalid", $"request={requestId}");
            return "Search query must not be empty.";
        }

        List<MediaSearchResult> results;
        try
        {
            results = await YtDlpService.SearchMediaAsync(query, platform, maxResults, cancellationToken).ConfigureAwait(false);
            DiagnosticLog.Write("search.success", $"request={requestId} count={results.Count}");
        }
        catch (ArgumentException ex)
        {
            DiagnosticLog.Write("search.invalid", $"request={requestId} error={ex.GetType().Name}");
            return ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            DiagnosticLog.Write("search.failed", $"request={requestId} error={ex.GetType().Name}");
            return $"Media search failed: {ex.Message}";
        }
        catch (TimeoutException)
        {
            DiagnosticLog.Write("search.timeout", $"request={requestId}");
            return "Media search timed out after 10 seconds. No results were returned; cancellation was requested for the search. Check get_media_diagnostics for the last completed stage.";
        }
        catch (OperationCanceledException)
        {
            DiagnosticLog.Write("search.canceled", $"request={requestId} callerCanceled={cancellationToken.IsCancellationRequested}");
            throw;
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("search.failed", $"request={requestId} error={ex.GetType().Name}");
            throw;
        }
        finally
        {
            DiagnosticLog.Write("search.end", $"request={requestId} elapsedMs={timer.ElapsedMilliseconds}");
        }

        if (results.Count == 0)
        {
            return $"No {platform} results were found for '{query}'.";
        }

        var lines = results.Select((result, index) => $"{index + 1}. {result.Title} — {result.Url}");
        return string.Join(Environment.NewLine, lines);
    }

    internal static string? ExtractVideoId(string videoUrlOrId)
    {
        if (VideoIdOnlyRegex().IsMatch(videoUrlOrId))
        {
            return videoUrlOrId;
        }

        var match = WatchUrlRegex().Match(videoUrlOrId);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        match = ShortUrlRegex().Match(videoUrlOrId);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdOnlyRegex();

    [GeneratedRegex(@"[?&]v=([A-Za-z0-9_-]{11})")]
    private static partial Regex WatchUrlRegex();

    [GeneratedRegex(@"youtu\.be/([A-Za-z0-9_-]{11})")]
    private static partial Regex ShortUrlRegex();
}

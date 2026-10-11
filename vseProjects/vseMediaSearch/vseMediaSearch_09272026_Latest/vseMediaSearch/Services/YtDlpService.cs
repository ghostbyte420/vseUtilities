using System.Diagnostics;
using System.Runtime.InteropServices;

namespace vseMediaSearch.Services;

/// <summary>
/// Downloads just the audio track of a video using "yt-dlp". If yt-dlp is already
/// installed and on PATH, that copy is used. Otherwise, this service automatically
/// downloads the official yt-dlp release binary (from its GitHub releases, where it is
/// distributed under the Unlicense/public domain) into a local cache folder under the
/// user's app-data directory, so no manual install step is required.
///
/// Heads up: using yt-dlp against YouTube is a ToS gray area. Auto-fetching it on the
/// user's behalf is a deliberate convenience tradeoff (per explicit user request), not a
/// legal guarantee of compliance — this is a pragmatic choice for personal/research use.
/// </summary>
internal static class YtDlpService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private static readonly string CacheFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseMediaSearch",
        "tools");

    private const string WindowsDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string UnixDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";

    /// <summary>
    /// Returns a yt-dlp <c>--cookies-from-browser</c> argument (with trailing space) when the
    /// VSEMEDIASEARCH_COOKIES_BROWSER environment variable names a browser (e.g. chrome, edge, firefox);
    /// otherwise an empty string. Helps get past YouTube's bot check.
    /// </summary>
    private static string CookieArguments()
    {
        // A Netscape-format cookies.txt file takes priority: it avoids browser DB locks and DPAPI/app-bound encryption.
        var cookieFile = Environment.GetEnvironmentVariable("VSEMEDIASEARCH_COOKIES_FILE");
        if (!string.IsNullOrWhiteSpace(cookieFile) && File.Exists(cookieFile.Trim()))
        {
            return $"--cookies \"{cookieFile.Trim()}\" ";
        }

        var browser = Environment.GetEnvironmentVariable("VSEMEDIASEARCH_COOKIES_BROWSER");
        return string.IsNullOrWhiteSpace(browser) ? string.Empty : $"--cookies-from-browser {browser.Trim()} ";
    }

    /// <summary>
    /// Downloads the audio track of the given video URL into <paramref name="outputFolder"/>
    /// and returns the full path to the resulting audio file.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if yt-dlp could not be located or fetched, or if the download fails.
    /// </exception>
    public static async Task<string> DownloadAudioAsync(
        string videoUrl,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        // Keep outputs owned by this request rather than picking another request's WAV.
        outputFolder = Path.Combine(outputFolder, "audio_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputFolder);

        var ytDlpPath = await ResolveYtDlpPathAsync(cancellationToken).ConfigureAwait(false);
        var audioTools = await FfmpegService.ResolveAudioToolsDirectoryAsync(cancellationToken).ConfigureAwait(false);

        var outputTemplate = Path.Combine(outputFolder, "%(id)s.%(ext)s");
        var arguments = $"{CookieArguments()}--no-playlist -x --ffmpeg-location \"{audioTools}\" --audio-format wav --postprocessor-args \"ExtractAudio+ffmpeg_o:-ar 16000 -ac 1 -c:a pcm_s16le\" -o \"{outputTemplate}\" \"{videoUrl}\"";

        var result = await RunProcessSelfContainedAsync(ytDlpPath, arguments, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"yt-dlp failed (exit code {result.ExitCode}): {result.StandardError}");
        }

        var wavFile = Directory.GetFiles(outputFolder, "*.wav")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return wavFile ?? throw new InvalidOperationException(
            $"yt-dlp reported success but no .wav file was found in {outputFolder}.");
    }

    /// <summary>
    /// Downloads the given video (video+audio, not just audio) into <paramref name="outputFolder"/>
    /// and returns the full path to the resulting video file. Used for OCR frame extraction,
    /// where the visual track (not just audio) is required.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if yt-dlp could not be located or fetched, or if the download fails.
    /// </exception>
    public static async Task<string> DownloadVideoAsync(
        string videoUrl,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputFolder);

        var ytDlpPath = await ResolveYtDlpPathAsync(cancellationToken).ConfigureAwait(false);

        var outputTemplate = Path.Combine(outputFolder, "%(id)s.%(ext)s");
        var arguments = $"{CookieArguments()}-f \"bestvideo[height<=720]+bestaudio/best[height<=720]\" -o \"{outputTemplate}\" \"{videoUrl}\"";

        var result = await RunProcessSelfContainedAsync(ytDlpPath, arguments, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"yt-dlp failed (exit code {result.ExitCode}): {result.StandardError}");
        }

        var videoFile = Directory.GetFiles(outputFolder)
            .Where(f => !f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return videoFile ?? throw new InvalidOperationException(
            $"yt-dlp reported success but no video file was found in {outputFolder}.");
    }

    /// <summary>
    /// Free, no-API-key search prefixes that yt-dlp itself understands for various media
    /// platforms (yt-dlp's "search engine" pseudo-URLs). Add new entries here as more
    /// yt-dlp-searchable platforms are wired in — no other code changes are required.
    /// </summary>
    private static readonly Dictionary<string, string> SearchPrefixesByPlatform = new(StringComparer.OrdinalIgnoreCase)
    {
        ["youtube"] = "ytsearch",
        ["soundcloud"] = "scsearch",
    };

    /// <summary>
    /// Searches a free, yt-dlp-supported media platform for videos/tracks matching
    /// <paramref name="query"/> using yt-dlp's built-in search pseudo-URLs (no API key or paid
    /// service required), returning up to <paramref name="maxResults"/> lightweight results
    /// (id/title/url) without downloading any media. See <see cref="SearchPrefixesByPlatform"/>
    /// for the set of currently supported platform values.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="platform"/> is not supported.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if yt-dlp could not be located or fetched, or if the search fails.
    /// </exception>
    public static Task<List<MediaSearchResult>> SearchMediaAsync(
        string query,
        string platform = "youtube",
        int maxResults = 5,
        CancellationToken cancellationToken = default)
        => SearchDeadline.RunAsync(
            token => SearchMediaCoreAsync(query, platform, maxResults, token),
            SearchDeadline.Budget, cancellationToken);

    private static async Task<List<MediaSearchResult>> SearchMediaCoreAsync(
        string query,
        string platform,
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (maxResults < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "maxResults must be at least 1.");
        }

        if (!SearchPrefixesByPlatform.TryGetValue(platform, out var searchPrefix))
        {
            var supported = string.Join(", ", SearchPrefixesByPlatform.Keys);
            throw new ArgumentException($"Unsupported platform '{platform}'. Supported platforms: {supported}.", nameof(platform));
        }

        var ytDlpPath = await ResolveYtDlpPathAsync(cancellationToken).ConfigureAwait(false);

        var searchTarget = $"{searchPrefix}{maxResults}:{query}";
        var arguments = $"--flat-playlist --dump-json \"{searchTarget}\"";

        var result = await RunProcessSelfContainedAsync(ytDlpPath, arguments, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"yt-dlp search failed (exit code {result.ExitCode}): {result.StandardError}");
        }

        var results = new List<MediaSearchResult>();
        foreach (var line in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            using var document = System.Text.Json.JsonDocument.Parse(line);
            var root = document.RootElement;

            var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
            var url = root.TryGetProperty("url", out var urlElement) ? urlElement.GetString() : null;
            var webpageUrl = root.TryGetProperty("webpage_url", out var webpageUrlElement) ? webpageUrlElement.GetString() : null;

            results.Add(new MediaSearchResult(
                Id: id,
                Title: title ?? id,
                Url: webpageUrl ?? url ?? id,
                Platform: platform));
        }

        return results;
    }

    /// <summary>
    /// Returns a usable path to a yt-dlp executable: an existing PATH install if found,
    /// otherwise a cached auto-downloaded copy (downloading it first if not already cached).
    /// </summary>
    private static async Task<string> ResolveYtDlpPathAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DiagnosticLog.Write("ytdlp.resolve.start");
        if (IsOnPath("yt-dlp"))
        {
            DiagnosticLog.Write("ytdlp.resolve.path");
            return "yt-dlp";
        }

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var cachedFileName = isWindows ? "yt-dlp.exe" : "yt-dlp";
        var cachedPath = Path.Combine(CacheFolder, cachedFileName);

        if (File.Exists(cachedPath))
        {
            DiagnosticLog.Write("ytdlp.resolve.cache");
            return cachedPath;
        }

        Directory.CreateDirectory(CacheFolder);
        DiagnosticLog.Write("ytdlp.download.start");
        var downloadUrl = isWindows ? WindowsDownloadUrl : UnixDownloadUrl;
        var temporaryPath = cachedPath + "." + Guid.NewGuid().ToString("N") + ".download";

        try
        {
            await using var responseStream = await Http.GetStreamAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
            await using (var fileStream = File.Create(temporaryPath))
            {
                await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!isWindows)
            {
                File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            try
            {
                File.Move(temporaryPath, cachedPath);
            }
            catch (IOException) when (File.Exists(cachedPath))
            {
                // Another request published a complete executable first; retain it.
                DiagnosticLog.Write("ytdlp.download.concurrent_cache");
            }
            DiagnosticLog.Write("ytdlp.download.complete");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not auto-download yt-dlp from {downloadUrl}. Check your network connection, " +
                "or install yt-dlp manually and ensure it is on PATH.", ex);
        }

        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DiagnosticLog.Write("ytdlp.download.cleanup_failed", $"error={ex.GetType().Name}");
            }
        }

        return cachedPath;
    }

    private static bool IsOnPath(string fileName)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var executableNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { fileName + ".exe" }
            : [fileName];

        foreach (var directory in pathVariable.Split(Path.PathSeparator))
        {
            foreach (var executableName in executableNames)
            {
                if (!string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, executableName)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Result of running a self-contained external process: exit code plus captured
    /// stdout/stderr text.
    /// </summary>
    private readonly record struct ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);

    /// <summary>
    /// Schedules process startup on the thread pool, then awaits exit asynchronously.
    /// Both output streams are drained concurrently. Cancellation attempts to stop the
    /// process tree; cleanup has a deadline in case termination fails.
    /// </summary>
    private static Task<ProcessRunResult> RunProcessSelfContainedAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var operationId = Guid.NewGuid().ToString("N");
            var timer = Stopwatch.StartNew();
            DiagnosticLog.Write("ytdlp.process.starting", $"operation={operationId}");
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            try
            {
                process.Start();
                // A child must never inherit or consume the MCP protocol input pipe.
                process.StandardInput.Close();
                DiagnosticLog.Write("ytdlp.process.started", $"operation={operationId} childPid={process.Id}");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                DiagnosticLog.Write("ytdlp.process.launch_failed", $"operation={operationId} nativeError={ex.NativeErrorCode}");
                throw new InvalidOperationException($"Failed to launch '{fileName}'.", ex);
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            var completion = Task.WhenAll(process.WaitForExitAsync(CancellationToken.None), stdoutTask, stderrTask);
            try
            {
                await completion.WaitAsync(cancellationToken).ConfigureAwait(false);
                DiagnosticLog.Write("ytdlp.process.exited", $"operation={operationId} exitCode={process.ExitCode}");
                return new ProcessRunResult(process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                DiagnosticLog.Write("ytdlp.process.canceled", $"operation={operationId}");
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                    DiagnosticLog.Write("ytdlp.process.kill_failed", $"operation={operationId} error={ex.GetType().Name}");
                }

                try
                {
                    await completion.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    DiagnosticLog.Write("ytdlp.process.cleanup_failed", $"operation={operationId} error={ex.GetType().Name}");
                    // Observe a later pipe-read failure if cleanup outlives the deadline.
                    _ = completion.ContinueWith(task => { _ = task.Exception; },
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }

                throw;
            }
            finally
            {
                DiagnosticLog.Write("ytdlp.process.end", $"operation={operationId} elapsedMs={timer.ElapsedMilliseconds}");
            }
        }, cancellationToken);
    }
}

/// <summary>
/// A single lightweight media search result: enough to identify and link to a video/track
/// without downloading it.
/// </summary>
internal sealed record MediaSearchResult(string Id, string Title, string Url, string Platform);

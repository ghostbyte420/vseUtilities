using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace vseMediaSearch.Services;

/// <summary>How <see cref="FfmpegService.ExtractFramesAsync"/> chooses which frames to keep.</summary>
internal enum FrameSamplingMode
{
    /// <summary>Sample one frame every N seconds, regardless of visual content.</summary>
    FixedInterval,

    /// <summary>Sample a frame only when the video content changes significantly (scene detection).</summary>
    SceneChange,
}

/// <summary>
/// Extracts still frames from a video/audio-adjacent media file at a fixed time interval,
/// using "ffmpeg". If ffmpeg is already installed and on PATH, that copy is used.
/// Otherwise, this service automatically downloads a static ffmpeg build into a local
/// cache folder under the user's app-data directory (Windows via gyan.dev's essentials
/// build, Linux via johnvansickle.com's static build), so no manual install step is
/// required on those platforms. macOS has no equivalent trivial static download source,
/// so an existing PATH install (e.g. via Homebrew) is required there.
/// </summary>
internal static class FfmpegService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private static readonly SemaphoreSlim InstallGate = new(1, 1);

    private static readonly string CacheFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseMediaSearch",
        "tools");

    private const string WindowsDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private const string LinuxDownloadUrl = "https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz";

    /// <summary>Finds a colocated executable pair for yt-dlp audio postprocessing.</summary>
    internal static async Task<string> ResolveAudioToolsDirectoryAsync(CancellationToken cancellationToken)
    {
        var suffix = OperatingSystem.IsWindows() ? ".exe" : "";
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var directory in path.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(directory) && Path.IsPathFullyQualified(directory)
                && HasAudioTools(directory, suffix))
            {
                return directory;
            }
        }

        await InstallGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!HasAudioTools(CacheFolder, suffix))
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new InvalidOperationException("Audio extraction requires ffmpeg and ffprobe together on PATH. Automatic audio-tool installation is currently supported on Windows only.");
                }

                Directory.CreateDirectory(CacheFolder);
                DiagnosticLog.Write("ffmpeg.install.start");
                await DownloadAndExtractWindowsZipAsync(Path.Combine(CacheFolder, "ffmpeg.exe"), cancellationToken).ConfigureAwait(false);
                DiagnosticLog.Write("ffmpeg.install.complete");
            }
            return CacheFolder;
        }
        finally
        {
            InstallGate.Release();
        }
    }

    internal static bool HasAudioTools(string directory, string suffix) =>
        File.Exists(Path.Combine(directory, "ffmpeg" + suffix))
        && File.Exists(Path.Combine(directory, "ffprobe" + suffix));

    /// <summary>
    /// Extracts frames from <paramref name="mediaFilePath"/> into <paramref name="outputFolder"/>
    /// as sequentially numbered JPEG images. In <see cref="FrameSamplingMode.FixedInterval"/> mode
    /// (the default), frames are sampled every <paramref name="intervalSeconds"/> seconds. In
    /// <see cref="FrameSamplingMode.SceneChange"/> mode, frames are sampled only when the visual
    /// content changes significantly (fewer, more meaningful frames on mostly-static content like
    /// a code walkthrough; <paramref name="intervalSeconds"/> is ignored in this mode).
    /// Returns the full paths of the extracted frame files, in order.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if ffmpeg could not be located/fetched, or if extraction fails.
    /// </exception>
    public static async Task<List<string>> ExtractFramesAsync(
        string mediaFilePath,
        string outputFolder,
        double intervalSeconds = 5.0,
        FrameSamplingMode samplingMode = FrameSamplingMode.FixedInterval,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputFolder);

        var ffmpegPath = await ResolveFfmpegPathAsync(cancellationToken).ConfigureAwait(false);

        var outputTemplate = Path.Combine(outputFolder, "frame-%05d.jpg");
        var filter = samplingMode == FrameSamplingMode.SceneChange
            ? "select='gt(scene,0.3)'"
            : $"fps={(1.0 / intervalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var arguments = $"-y -i \"{mediaFilePath}\" -vf \"{filter}\" -vsync vfr -q:v 2 \"{outputTemplate}\"";

        var result = await RunProcessSelfContainedAsync(ffmpegPath, arguments, cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed (exit code {result.ExitCode}): {result.StandardError}");
        }

        return [.. Directory.GetFiles(outputFolder, "frame-*.jpg").OrderBy(f => f, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Returns a usable path to an ffmpeg executable: an existing PATH install if found,
    /// otherwise a cached auto-downloaded copy (downloading/extracting it first if not cached).
    /// </summary>
    private static async Task<string> ResolveFfmpegPathAsync(CancellationToken cancellationToken)
    {
        if (IsOnPath("ffmpeg"))
        {
            return "ffmpeg";
        }

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        var cachedFileName = isWindows ? "ffmpeg.exe" : "ffmpeg";
        var cachedPath = Path.Combine(CacheFolder, cachedFileName);

        if (File.Exists(cachedPath))
        {
            return cachedPath;
        }

        if (!isWindows && !isLinux)
        {
            throw new InvalidOperationException(
                "ffmpeg was not found on PATH. Auto-download is only supported on Windows/Linux; " +
                "on macOS please install ffmpeg (e.g. 'brew install ffmpeg') and ensure it is on PATH.");
        }

        Directory.CreateDirectory(CacheFolder);

        if (isWindows)
        {
            await DownloadAndExtractWindowsZipAsync(cachedPath, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await DownloadAndExtractLinuxTarAsync(cachedPath, cancellationToken).ConfigureAwait(false);
            var chmod = Process.Start(new ProcessStartInfo("chmod", $"+x \"{cachedPath}\"")
            {
                UseShellExecute = false,
            });
            if (chmod is not null)
            {
                await chmod.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return cachedPath;
    }

    private static async Task DownloadAndExtractWindowsZipAsync(string cachedPath, CancellationToken cancellationToken)
    {
        var tempZipPath = Path.Combine(CacheFolder, $"ffmpeg-download-{Guid.NewGuid():N}.zip");
        var tempExtractFolder = Path.Combine(CacheFolder, $"ffmpeg-extract-{Guid.NewGuid():N}");

        try
        {
            try
            {
                await using var responseStream = await Http.GetStreamAsync(WindowsDownloadUrl, cancellationToken).ConfigureAwait(false);
                await using var fileStream = File.Create(tempZipPath);
                await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Could not auto-download ffmpeg from {WindowsDownloadUrl}. Check your network connection, " +
                    "or install ffmpeg manually and ensure it is on PATH.", ex);
            }

            ZipFile.ExtractToDirectory(tempZipPath, tempExtractFolder);

            var extractedExe = Directory.GetFiles(tempExtractFolder, "ffmpeg.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
            var extractedProbe = Directory.GetFiles(tempExtractFolder, "ffprobe.exe", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (extractedExe is null || extractedProbe is null)
            {
                throw new InvalidOperationException("Downloaded ffmpeg archive must contain both ffmpeg.exe and ffprobe.exe.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(extractedProbe, Path.Combine(Path.GetDirectoryName(cachedPath)!, "ffprobe.exe"), overwrite: true);
            File.Move(extractedExe, cachedPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }

            if (Directory.Exists(tempExtractFolder))
            {
                Directory.Delete(tempExtractFolder, recursive: true);
            }
        }
    }

    private static async Task DownloadAndExtractLinuxTarAsync(string cachedPath, CancellationToken cancellationToken)
    {
        var tempExtractFolder = Path.Combine(CacheFolder, $"ffmpeg-extract-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempExtractFolder);

        try
        {
            Stream responseStream;

            try
            {
                responseStream = await Http.GetStreamAsync(LinuxDownloadUrl, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Could not auto-download ffmpeg from {LinuxDownloadUrl}. Check your network connection, " +
                    "or install ffmpeg manually and ensure it is on PATH.", ex);
            }

            await using (responseStream)
            await using (var decompressStream = new System.IO.Compression.GZipStream(responseStream, CompressionMode.Decompress))
            {
                await TarFile.ExtractToDirectoryAsync(decompressStream, tempExtractFolder, overwriteFiles: true, cancellationToken)
                    .ConfigureAwait(false);
            }

            var extractedBinary = Directory.GetFiles(tempExtractFolder, "ffmpeg", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (extractedBinary is null)
            {
                throw new InvalidOperationException("Downloaded ffmpeg archive did not contain an ffmpeg binary.");
            }

            File.Copy(extractedBinary, cachedPath, overwrite: true);
        }
        finally
        {
            if (Directory.Exists(tempExtractFolder))
            {
                Directory.Delete(tempExtractFolder, recursive: true);
            }
        }
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
    /// Result of running a self-contained external process: exit code plus captured stderr text.
    /// </summary>
    private readonly record struct ProcessRunResult(int ExitCode, string StandardError);

    /// <summary>
    /// Launches ffmpeg fully isolated on its own dedicated background thread (via
    /// <see cref="Task.Run(Func{Task{ProcessRunResult}})"/>), so the calling MCP tool call
    /// never blocks the shared thread pool/synchronization context while ffmpeg runs.
    /// The child process is forcibly killed (whole process tree) if the operation is
    /// canceled, so no orphaned ffmpeg process is left running in the background.
    /// </summary>
    private static Task<ProcessRunResult> RunProcessSelfContainedAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException($"Failed to launch '{fileName}'.", ex);
            }

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Best-effort kill; process may have already exited.
                }
            });

            var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            var stderr = await stderrTask.ConfigureAwait(false);

            return new ProcessRunResult(process.ExitCode, stderr);
        }, cancellationToken);
    }
}

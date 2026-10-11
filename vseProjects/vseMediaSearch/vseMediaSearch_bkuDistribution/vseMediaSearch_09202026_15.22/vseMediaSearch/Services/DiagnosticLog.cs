using System.Globalization;
using System.Text;

namespace vseMediaSearch.Services;

/// <summary>Small local diagnostic log; never writes to MCP's standard output.</summary>
internal static class DiagnosticLog
{
    private const int MaxFileBytes = 262144;
    private static readonly object Gate = new();
    internal static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseMediaSearch", "diagnostics", "server.log");

    internal static void Write(string stage, string detail = "")
    {
        // Do not record search text, URLs, transcripts, arguments, or exception messages.
        var safeDetail = detail.Replace('\r', ' ').Replace('\n', ' ');
        if (safeDetail.Length > 1024)
        {
            safeDetail = safeDetail[..1024];
        }

        var line = string.Create(CultureInfo.InvariantCulture,
            $"{DateTimeOffset.UtcNow:O} pid={Environment.ProcessId} {stage} {safeDetail}{Environment.NewLine}");
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length + Encoding.UTF8.GetByteCount(line) > MaxFileBytes)
                {
                    // Keep at most the current file and one bounded previous file.
                    File.Move(LogPath, LogPath + ".previous", overwrite: true);
                }

                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Diagnostic log unavailable: {ex.GetType().Name}");
        }
    }

    internal static string ReadRecent()
    {
        try
        {
            lock (Gate)
            {
                if (!File.Exists(LogPath))
                {
                    return "No diagnostic entries have been written yet.";
                }

                using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var bytes = new byte[(int)Math.Min(stream.Length, 16384)];
                stream.Seek(-bytes.Length, SeekOrigin.End);
                var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
                return Encoding.UTF8.GetString(bytes, 0, read);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Cannot read diagnostics: {ex.GetType().Name}.";
        }
    }
}

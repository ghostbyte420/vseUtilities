using System.Runtime.InteropServices;

namespace vseMediaSearch.Services;

/// <summary>
/// Resolves the current user's real OS "Downloads" folder (not a hardcoded drive/path) and
/// creates a per-session subfolder under it for any media this MCP server downloads
/// (e.g. audio extracted for transcription). The caller (the user) owns cleanup of that
/// folder; this server never auto-deletes it.
/// </summary>
internal static class DownloadFolderResolver
{
    // Windows "Downloads" known folder GUID (FOLDERID_Downloads). Not exposed via
    // Environment.SpecialFolder, so it must be resolved through the shell API on Windows.
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
        uint dwFlags,
        nint hToken,
        out nint pszPath);

    /// <summary>
    /// Returns the OS's real Downloads folder path for the current user.
    /// </summary>
    public static string GetDownloadsFolder()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var result = SHGetKnownFolderPath(DownloadsFolderId, 0, 0, out var pathPtr);
            if (result == 0 && pathPtr != nint.Zero)
            {
                try
                {
                    var path = Marshal.PtrToStringUni(pathPtr);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pathPtr);
                }
            }
        }

        // Fallback for non-Windows platforms (and as a safety net if the shell call fails):
        // the conventional ~/Downloads folder used by macOS/Linux and most Windows setups.
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, "Downloads");
    }

    /// <summary>
    /// Creates (if needed) and returns a dedicated session subfolder inside the real Downloads
    /// folder, e.g. "&lt;Downloads&gt;/vseMediaSearch/session_20260115_142233". The caller controls
    /// deletion of this folder; it is never deleted automatically by this server.
    /// </summary>
    public static string GetOrCreateSessionFolder(string sessionId)
    {
        var downloads = GetDownloadsFolder();
        var sessionFolder = Path.Combine(downloads, "vseMediaSearch", sessionId);
        Directory.CreateDirectory(sessionFolder);
        return sessionFolder;
    }
}

using System.ComponentModel;
using ModelContextProtocol.Server;
using vseMediaSearch.Services;

namespace vseMediaSearch.Tools;

[McpServerToolType]
internal sealed class DiagnosticsTools
{
    [McpServerTool(Name = "get_media_diagnostics")]
    [Description("Returns the current media server process ID, loaded assembly identity, and up to 16 KiB of recent local diagnostic events. Does not start downloads, searches, or transcription.")]
    public static string GetMediaDiagnostics()
    {
        var assembly = typeof(DiagnosticsTools).Assembly;
        return $"PID: {Environment.ProcessId}\nAssembly: {assembly.Location}\nBuild ID: {assembly.ManifestModule.ModuleVersionId}\nLog: {DiagnosticLog.LogPath}\n{DiagnosticLog.ReadRecent()}";
    }
}

using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

/// <summary>
/// Read-only tools that help explain what an unfamiliar file is and how it is used.
/// </summary>
internal class BinaryAnalysisTools
{
    [McpServerTool]
    [Description("Identifies what kind of file this is from its header bytes (Windows program/DLL, UOP archive, PNG, ZIP, text, or UNKNOWN) and explains it in plain 'like you're 3' words. For Windows programs it reports 32/64-bit, build date, .NET or native, system libraries used and readable text inside. Says UNKNOWN honestly instead of guessing. Read-only.")]
    public string AnalyzeFile(
        [Description("Full path to the file to identify.")] string filePath)
    {
        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        try
        {
            return BinaryAnalyzer.Analyze(filePath);
        }
        catch (Exception ex)
        {
            return $"Failed to analyze {filePath}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Shows a section of any file as labeled 32-bit numbers (decimal and hex), 16-bit pairs and text side by side, and flags 'count, then that many numbered pairs' patterns. Use for data files with no known format. Read-only.")]
    public string ViewBinaryAsValues(
        [Description("Full path to the file.")] string filePath,
        [Description("Byte offset to start at.")] long offset = 0,
        [Description("How many bytes to show (4 to 512, rounded down to a multiple of 4).")] int length = 128)
    {
        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        try
        {
            return BinaryAnalyzer.ViewAsValues(filePath, offset, length);
        }
        catch (Exception ex)
        {
            return $"Failed to read {filePath}: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Searches the C# source files in a folder for code that mentions a given file name (whole-word, so 'wantX' does not match 'antX'). Separates likely file references (inside string literals) from other mentions, and says clearly when nothing is found. Read-only.")]
    public string FindFileUsageInSource(
        [Description("Full path to the source folder to search, e.g. the uoAvox Source folder.")] string sourceFolder,
        [Description("File name to look for, e.g. housing.bin.")] string fileName,
        [Description("Maximum results to list per group.")] int maxResults = 30)
    {
        if (!Directory.Exists(sourceFolder))
            return $"Folder not found: {sourceFolder}";

        try
        {
            return BinaryAnalyzer.FindUsage(sourceFolder, fileName, maxResults);
        }
        catch (Exception ex)
        {
            return $"Search failed: {ex.Message}";
        }
    }
}

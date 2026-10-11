using ModelContextProtocol.Server;
using System.ComponentModel;

internal class BundleTools
{
    private const string DefaultOut = @"C:\Users\aasro\Desktop\vseDecompiled";

    [McpServerTool(Name = "list_bundle_contents")]
    [Description("Read-only. Detects whether an .exe is a .NET single-file bundle (a native launcher with managed assemblies packed inside) and lists the packed files. Reports honestly if it is not a bundle.")]
    public string ListBundleContents(
        [Description("Full path to the .exe.")] string exePath,
        [Description("Maximum entries to list. Default 300.")] int maxResults = 300)
        => BundleReader.List(exePath, maxResults);

    [McpServerTool(Name = "extract_bundle")]
    [Description("Extracts the files packed inside a .NET single-file bundle to a new '<name>_bundle' subfolder so the assemblies can be decompiled. Never modifies the original; refuses to write into a non-empty folder.")]
    public string ExtractBundle(
        [Description("Full path to the bundled .exe.")] string exePath,
        [Description("Folder to extract into. Defaults to C:\\Users\\aasro\\Desktop\\vseDecompiled.")] string? outputFolder = null,
        [Description("If true (default), extract only managed assemblies, skipping runtime/native files.")] bool assembliesOnly = true)
        => BundleReader.Extract(exePath, string.IsNullOrWhiteSpace(outputFolder) ? DefaultOut : outputFolder, assembliesOnly);
}

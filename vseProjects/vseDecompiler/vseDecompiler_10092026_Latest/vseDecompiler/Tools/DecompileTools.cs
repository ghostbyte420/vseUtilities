using ModelContextProtocol.Server;
using System.ComponentModel;

internal class DecompileTools
{
    private const string DefaultOut = @"C:\Users\aasro\Desktop\vseDecompiled";

    [McpServerTool(Name = "list_types")]
    [Description("Read-only. Lists the top-level types (classes, structs, enums, interfaces) in a .NET assembly (.dll/.exe). Reports honestly if the file is native.")]
    public string ListTypes(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Optional case-insensitive text the type name must contain.")] string? filter = null,
        [Description("Maximum types to list. Default 200.")] int maxResults = 200)
        => AssemblyDecompiler.ListTypes(assemblyPath, filter, maxResults);

    [McpServerTool(Name = "decompile_type")]
    [Description("Read-only. Decompiles one type from a .NET assembly to C# source text using the ILSpy engine and returns it. Writes nothing.")]
    public string DecompileType(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Full type name, e.g. MyApp.Services.Foo (from list_types).")] string fullTypeName)
        => AssemblyDecompiler.DecompileType(assemblyPath, fullTypeName);

    [McpServerTool(Name = "decompile_assembly_to_folder")]
    [Description("Decompiles a whole .NET assembly into a C# project folder (like ILSpy 'Save Code'). Writes ONLY to a new/empty subfolder of outputFolder; never modifies the original; refuses to overwrite.")]
    public string DecompileAssemblyToFolder(
        [Description("Full path to the .NET .dll or .exe.")] string assemblyPath,
        [Description("Folder to export into. Defaults to C:\\Users\\aasro\\Desktop\\vseDecompiled.")] string? outputFolder = null)
        => AssemblyDecompiler.ExportProject(assemblyPath, string.IsNullOrWhiteSpace(outputFolder) ? DefaultOut : outputFolder);
}

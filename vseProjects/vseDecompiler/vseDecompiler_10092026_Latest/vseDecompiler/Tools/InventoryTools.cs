using ModelContextProtocol.Server;
using System.ComponentModel;

internal class InventoryTools
{
    [McpServerTool(Name = "inventory_project")]
    [Description("Read-only scan of a folder. Counts files per programming language and identifies every .exe/.dll as .NET (decompilable) or native, with 32/64-bit, type and target framework. Never modifies anything.")]
    public string InventoryProject(
        [Description("Full path of the folder to scan, e.g. a project or repository root.")] string folderPath,
        [Description("If true, also scans bin, obj, .git, node_modules and packages folders. Default false.")] bool includeBuildOutput = false,
        [Description("Maximum number of executables/libraries to list. Default 200.")] int maxListed = 200)
        => ProjectInventory.Run(folderPath, includeBuildOutput, maxListed);
}

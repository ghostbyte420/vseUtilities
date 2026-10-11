using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using vseUtilities.Models;

namespace vseUtilities.Services;

/// <summary>
/// Merges selected MCP tools' "servers" entries into a single .mcp.json file.
/// </summary>
public static class McpExportService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Builds the merged { "servers": { ... } } document for the given tools.
    /// Later entries with a duplicate server name overwrite earlier ones.
    /// </summary>
    public static JsonObject BuildMergedDocument(IEnumerable<McpToolInfo> selectedTools)
    {
        var mergedServers = new JsonObject();

        foreach (var tool in selectedTools)
        {
            foreach (var (serverName, serverConfig) in tool.ServersObject)
            {
                // DeepClone is required because a JsonNode can only be attached
                // to one parent JsonObject at a time.
                mergedServers[serverName] = serverConfig?.DeepClone();
            }
        }

        return new JsonObject
        {
            ["servers"] = mergedServers,
        };
    }

    /// <summary>
    /// Writes the merged document as "&lt;destinationDirectory&gt;\.mcp.json".
    /// Returns the full path of the file written.
    /// </summary>
    public static string ExportToDirectory(IEnumerable<McpToolInfo> selectedTools, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        var document = BuildMergedDocument(selectedTools);
        var outputPath = Path.Combine(destinationDirectory, ".mcp.json");

        File.WriteAllText(outputPath, document.ToJsonString(WriteOptions));

        return outputPath;
    }
}

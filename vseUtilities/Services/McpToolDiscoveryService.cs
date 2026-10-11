using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using vseUtilities.Models;

namespace vseUtilities.Services;

/// <summary>
/// Discovers MCP tool projects by scanning each top-level subfolder of
/// vseProjects for its actual .csproj file, always building the server entry
/// fresh from whatever is on disk right now (never trusting any leftover
/// .mcp.json content, which can go stale as tools move/get republished).
/// </summary>
public static class McpToolDiscoveryService
{
    /// <summary>
    /// Resolves the "vseProjects" directory that always sits alongside
    /// vseUtilities.exe in a proper deployment. Creates it if it doesn't
    /// exist yet, so first-run installs still have somewhere to drop tools.
    /// </summary>
    public static string? FindVseProjectsRoot(string startDirectory)
    {
        var candidate = Path.Combine(startDirectory, "vseProjects");

        try
        {
            if (!Directory.Exists(candidate))
            {
                Directory.CreateDirectory(candidate);
            }

            return candidate;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Scans each top-level subfolder of <paramref name="vseProjectsRoot"/>
    /// and builds the MCP server entry directly from whatever .csproj is
    /// actually found inside it right now - never trusting stale/leftover
    /// .mcp.json files, whose recorded paths can drift out of date if the
    /// tool gets moved, renamed, or re-published under a new "_Latest" folder.
    /// </summary>
    /// <param name="vseProjectsRoot">The vseProjects directory to scan.</param>
    /// <param name="skippedToolNames">
    /// Receives the folder names of any top-level tool folder that doesn't
    /// contain a matching .csproj anywhere inside it, so the caller can
    /// surface this to the user instead of the tool silently vanishing.
    /// </param>
    public static List<McpToolInfo> DiscoverTools(string vseProjectsRoot, out List<string> skippedToolNames)
    {
        var results = new List<McpToolInfo>();
        skippedToolNames = new List<string>();

        if (!Directory.Exists(vseProjectsRoot))
        {
            return results;
        }

        foreach (var toolDir in Directory.EnumerateDirectories(vseProjectsRoot))
        {
            var toolName = Path.GetFileName(toolDir);
            var csprojPath = FindCsprojForTool(toolDir, toolName);

            if (csprojPath is not null)
            {
                var tool = CreateToolFromCsproj(csprojPath);
                results.Add(new McpToolInfo
                {
                    Name = toolName,
                    McpJsonPath = csprojPath,
                    LastModifiedUtc = tool.LastModifiedUtc,
                    ServersObject = tool.ServersObject,
                });
                continue;
            }

            // No .csproj found (e.g. a source-less deployment) - fall back to
            // a built .exe for this tool, so pre-published copies still work.
            var exePath = FindExeForTool(toolDir, toolName);
            if (exePath is null)
            {
                skippedToolNames.Add(toolName);
                continue;
            }

            var exeTool = CreateToolFromExe(exePath);
            results.Add(new McpToolInfo
            {
                Name = toolName,
                McpJsonPath = exePath,
                LastModifiedUtc = exeTool.LastModifiedUtc,
                ServersObject = exeTool.ServersObject,
            });
        }

        return results.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Finds the .csproj that represents <paramref name="toolName"/> inside
    /// its top-level folder, searching recursively (skipping bin/obj output
    /// folders). Prefers a .csproj whose file name matches the tool folder's
    /// name exactly; falls back to the most recently modified .csproj found
    /// anywhere under the folder if no exact name match exists.
    /// </summary>
    private static string? FindCsprojForTool(string toolDir, string toolName)
    {
        var candidates = Directory.EnumerateFiles(toolDir, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsInBuildOutputFolder(path))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var exactMatch = candidates.FirstOrDefault(path =>
            string.Equals(Path.GetFileNameWithoutExtension(path), toolName, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
        {
            return exactMatch;
        }

        return candidates
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
    }

    private static bool IsInBuildOutputFolder(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(s => s.Equals("bin", StringComparison.OrdinalIgnoreCase)
            || s.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds a built .exe for <paramref name="toolName"/> anywhere under its
    /// top-level folder (typically under bin/Release or bin/.../publish),
    /// since this is the fallback used when no .csproj is present. Prefers
    /// an exe whose file name matches the tool folder's name exactly; falls
    /// back to the most recently modified .exe found anywhere under the
    /// folder if no exact name match exists.
    /// </summary>
    private static string? FindExeForTool(string toolDir, string toolName)
    {
        var candidates = Directory.EnumerateFiles(toolDir, "*.exe", SearchOption.AllDirectories).ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var exactMatch = candidates.FirstOrDefault(path =>
            string.Equals(Path.GetFileNameWithoutExtension(path), toolName, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
        {
            return exactMatch;
        }

        return candidates
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
    }

    /// <summary>
    /// Builds a McpToolInfo for a tool discovered by browsing directly to its
    /// .csproj file (used by the "Add MCP" button), synthesizing the same
    /// stdio/dotnet-run server shape used throughout this solution.
    /// </summary>
    public static McpToolInfo CreateToolFromCsproj(string csprojPath)
    {
        var toolName = Path.GetFileNameWithoutExtension(csprojPath);

        var serverConfig = new JsonObject
        {
            ["type"] = "stdio",
            ["command"] = "dotnet",
            ["args"] = new JsonArray("run", "--project", csprojPath),
        };

        var serversNode = new JsonObject
        {
            [toolName] = serverConfig,
        };

        return new McpToolInfo
        {
            Name = toolName,
            McpJsonPath = csprojPath,
            LastModifiedUtc = File.GetLastWriteTimeUtc(csprojPath),
            ServersObject = serversNode,
        };
    }

    /// <summary>
    /// Builds a McpToolInfo for a tool discovered by browsing directly to (or
    /// auto-finding) its built .exe file (used by the "Add MCP" button and
    /// by auto-discovery when no .csproj is present), synthesizing the same
    /// stdio server shape used throughout this solution but launching the
    /// exe directly instead of "dotnet run --project".
    /// </summary>
    public static McpToolInfo CreateToolFromExe(string exePath)
    {
        var toolName = Path.GetFileNameWithoutExtension(exePath);

        var serverConfig = new JsonObject
        {
            ["type"] = "stdio",
            ["command"] = exePath,
            ["args"] = new JsonArray(),
        };

        var serversNode = new JsonObject
        {
            [toolName] = serverConfig,
        };

        return new McpToolInfo
        {
            Name = toolName,
            McpJsonPath = exePath,
            LastModifiedUtc = File.GetLastWriteTimeUtc(exePath),
            ServersObject = serversNode,
        };
    }
}

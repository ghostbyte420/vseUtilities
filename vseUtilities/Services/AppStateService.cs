using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using vseUtilities.Models;

namespace vseUtilities.Services;

/// <summary>
/// Persists the user's MCP tool list (including manually-added tools and
/// checkbox selection state) across app restarts, so the checklist doesn't
/// reset back to whatever vseProjects happens to contain on launch.
/// </summary>
public static class AppStateService
{
    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseUtilities",
        "tools-state.json");

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    private sealed class PersistedTool
    {
        public string Name { get; set; } = string.Empty;

        public string McpJsonPath { get; set; } = string.Empty;

        public bool IsSelected { get; set; }

        [JsonConverter(typeof(JsonNodeAsObjectConverter))]
        public JsonObject? ServersObject { get; set; }
    }

    /// <summary>
    /// Loads whatever tool list was last saved, or an empty list if no state
    /// file exists yet (first run) or it's corrupted.
    /// </summary>
    public static List<McpToolInfo> Load()
    {
        var result = new List<McpToolInfo>();

        if (!File.Exists(StateFilePath))
        {
            return result;
        }

        try
        {
            var json = File.ReadAllText(StateFilePath);
            var persisted = JsonSerializer.Deserialize<List<PersistedTool>>(json);
            if (persisted is null)
            {
                return result;
            }

            foreach (var tool in persisted)
            {
                if (tool.ServersObject is null || string.IsNullOrWhiteSpace(tool.McpJsonPath))
                {
                    continue;
                }

                result.Add(new McpToolInfo
                {
                    Name = tool.Name,
                    McpJsonPath = tool.McpJsonPath,
                    LastModifiedUtc = File.Exists(tool.McpJsonPath)
                        ? File.GetLastWriteTimeUtc(tool.McpJsonPath)
                        : DateTime.UtcNow,
                    ServersObject = tool.ServersObject.DeepClone().AsObject(),
                    IsSelected = tool.IsSelected,
                });
            }
        }
        catch (Exception)
        {
            // Corrupted state file - start fresh rather than crashing the app.
        }

        return result;
    }

    /// <summary>
    /// Writes the given tool list to disk, overwriting whatever was saved
    /// previously. Called after every add/delete/selection change.
    /// </summary>
    public static void Save(IEnumerable<McpToolInfo> tools)
    {
        try
        {
            var directory = Path.GetDirectoryName(StateFilePath)!;
            Directory.CreateDirectory(directory);

            var persisted = tools.Select(t => new PersistedTool
            {
                Name = t.Name,
                McpJsonPath = t.McpJsonPath,
                IsSelected = t.IsSelected,
                ServersObject = t.ServersObject.DeepClone().AsObject(),
            }).ToList();

            File.WriteAllText(StateFilePath, JsonSerializer.Serialize(persisted, WriteOptions));
        }
        catch (Exception)
        {
            // Best effort - a failed save shouldn't crash the app.
        }
    }

    /// <summary>
    /// System.Text.Json doesn't know how to (de)serialize JsonObject as a
    /// plain property out of the box in older converters, but Nodes support
    /// it directly - this converter just delegates to JsonNode parsing.
    /// </summary>
    private sealed class JsonNodeAsObjectConverter : JsonConverter<JsonObject?>
    {
        public override JsonObject? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            return JsonNode.Parse(doc.RootElement.GetRawText())?.AsObject();
        }

        public override void Write(Utf8JsonWriter writer, JsonObject? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }
    }
}

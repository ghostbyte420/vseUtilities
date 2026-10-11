using System;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;

namespace vseUtilities.Models;

/// <summary>
/// Represents a discovered MCP tool project, backed by a per-project .mcp.json file.
/// </summary>
public partial class McpToolInfo : ObservableObject
{
    /// <summary>
    /// Raised whenever this tool's checkbox is toggled, so the owning list
    /// can re-sort itself (checked tools first) without polling.
    /// </summary>
    public event EventHandler? SelectionChanged;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// Auto-generated hook (via [ObservableProperty]) that fires every time
    /// IsSelected changes - used here to notify listeners for re-sorting.
    /// </summary>
    partial void OnIsSelectedChanged(bool value)
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public required string Name { get; init; }

    public required string McpJsonPath { get; init; }

    public required DateTime LastModifiedUtc { get; init; }

    /// <summary>
    /// The raw "servers" JSON object extracted from this tool's .mcp.json file.
    /// </summary>
    public required JsonObject ServersObject { get; init; }
}

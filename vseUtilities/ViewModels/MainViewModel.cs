using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using vseUtilities.Models;
using vseUtilities.Services;

namespace vseUtilities.ViewModels;

/// <summary>
/// Drives the main window: discovers MCP tools on launch, lets the user
/// check/uncheck them, and exports the selected tools' merged servers
/// config to a chosen folder.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    /// <summary>
    /// Function that asks the view (window) to prompt the user for an export
    /// destination folder. Wired up by the View so the ViewModel stays free
    /// of any Avalonia UI-control dependencies.
    /// </summary>
    public Func<Task<string?>>? RequestExportFolder { get; set; }

    /// <summary>
    /// Function that asks the view (window) to prompt the user to browse for
    /// a .csproj file when manually adding a new MCP tool.
    /// </summary>
    public Func<Task<string?>>? RequestCsprojFile { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<McpToolInfo> Tools { get; set; } = new();

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    public MainViewModel()
    {
        RefreshTools();
    }

    /// <summary>
    /// Auto-generated hook (via [ObservableProperty]) that fires just before
    /// Tools is replaced, so we can detach event handlers from the outgoing
    /// collection and avoid leaking duplicate subscriptions.
    /// </summary>
    partial void OnToolsChanging(ObservableCollection<McpToolInfo> value)
    {
        foreach (var tool in Tools)
        {
            tool.SelectionChanged -= OnToolSelectionChanged;
        }
    }

    /// <summary>
    /// Auto-generated hook (via [ObservableProperty]) that fires whenever the
    /// Tools collection itself is replaced (e.g. by RefreshTools), so every
    /// tool in the new collection gets wired up to trigger re-sorting.
    /// </summary>
    partial void OnToolsChanged(ObservableCollection<McpToolInfo> value)
    {
        foreach (var tool in value)
        {
            tool.SelectionChanged += OnToolSelectionChanged;
        }
    }

    /// <summary>
    /// Moves checked tools to the top of the list and unchecked tools below,
    /// preserving relative order within each group, so the user can see
    /// their selections at a glance without scrolling to find them.
    /// </summary>
    private void OnToolSelectionChanged(object? sender, EventArgs e)
    {
        var reordered = Tools
            .OrderByDescending(t => t.IsSelected)
            .ToList();

        Tools = new ObservableCollection<McpToolInfo>(reordered);
        AppStateService.Save(Tools);
    }

    /// <summary>
    /// Re-scans the vseProjects folder for MCP tools. Called on startup and
    /// available to re-run manually so "last modified" stays current.
    /// </summary>
    [RelayCommand]
    public void RefreshTools()
    {
        var projectsRoot = McpToolDiscoveryService.FindVseProjectsRoot(AppContext.BaseDirectory);

        var persisted = AppStateService.Load();
        var persistedByPath = persisted
            .GroupBy(t => t.McpJsonPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var discovered = new List<McpToolInfo>();
        var skippedToolNames = new List<string>();

        if (projectsRoot is not null)
        {
            discovered = McpToolDiscoveryService.DiscoverTools(projectsRoot, out skippedToolNames);
        }

        // Restore each auto-discovered tool's previous checkbox state.
        foreach (var tool in discovered)
        {
            if (persistedByPath.TryGetValue(tool.McpJsonPath, out var savedTool))
            {
                tool.IsSelected = savedTool.IsSelected;
            }
        }

        // Keep any manually-added tools that aren't part of the vseProjects
        // scan (e.g. added via "Add MCP" by browsing to a .csproj elsewhere).
        // Drop persisted entries whose backing file no longer exists, or that
        // share a name with a freshly-discovered tool (the scan is always
        // authoritative for anything actually found under vseProjects).
        var discoveredPaths = new HashSet<string>(discovered.Select(t => t.McpJsonPath), StringComparer.OrdinalIgnoreCase);
        var discoveredNames = new HashSet<string>(discovered.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
        var manuallyAdded = persisted
            .Where(t => !discoveredPaths.Contains(t.McpJsonPath))
            .Where(t => !discoveredNames.Contains(t.Name))
            .Where(t => File.Exists(t.McpJsonPath))
            .ToList();

        Tools = new ObservableCollection<McpToolInfo>(discovered.Concat(manuallyAdded));

        if (projectsRoot is null)
        {
            StatusMessage = Tools.Count > 0
                ? "Could not locate/create the vseProjects folder. Showing previously saved tools."
                : "Could not locate/create the vseProjects folder.";
            return;
        }

        StatusMessage = skippedToolNames.Count > 0
            ? $"Found {Tools.Count} MCP tool(s). Skipped invalid .mcp.json in: {string.Join(", ", skippedToolNames)}"
            : $"Found {Tools.Count} MCP tool(s).";
    }

    /// <summary>
    /// Lets the user browse to any .csproj or built .exe on disk and adds it
    /// to the checklist immediately. A .csproj is added as a stdio/dotnet-run
    /// server (same shape as auto-discovered tools); an .exe is added as a
    /// stdio server that launches the exe directly, no "dotnet run" needed.
    /// </summary>
    [RelayCommand]
    private async Task AddMcpAsync()
    {
        if (RequestCsprojFile is null)
        {
            StatusMessage = "File picker is not available.";
            return;
        }

        var filePath = await RequestCsprojFile();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            StatusMessage = "Add MCP cancelled.";
            return;
        }

        if (Tools.Any(t => string.Equals(t.McpJsonPath, filePath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "That tool has already been added.";
            return;
        }

        try
        {
            var isExe = string.Equals(Path.GetExtension(filePath), ".exe", StringComparison.OrdinalIgnoreCase);
            var tool = isExe
                ? McpToolDiscoveryService.CreateToolFromExe(filePath)
                : McpToolDiscoveryService.CreateToolFromCsproj(filePath);
            tool.SelectionChanged += OnToolSelectionChanged;
            Tools.Add(tool);
            AppStateService.Save(Tools);
            StatusMessage = $"Added {tool.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add tool: {ex.Message}";
        }
    }

    /// <summary>
    /// Removes every checked tool from the list (list only - this does not
    /// touch anything on disk). Mirrors ExportAsync's "act on whatever is
    /// checked" pattern, but removes instead of exporting.
    /// </summary>
    [RelayCommand]
    private void DeleteMcp()
    {
        var selectedTools = Tools.Where(t => t.IsSelected).ToList();

        if (selectedTools.Count == 0)
        {
            StatusMessage = "Select at least one tool before deleting.";
            return;
        }

        foreach (var tool in selectedTools)
        {
            tool.SelectionChanged -= OnToolSelectionChanged;
            Tools.Remove(tool);
        }

        AppStateService.Save(Tools);
        StatusMessage = $"Removed {selectedTools.Count} tool(s) from the list.";
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var selectedTools = Tools.Where(t => t.IsSelected).ToList();

        if (selectedTools.Count == 0)
        {
            StatusMessage = "Select at least one tool before exporting.";
            return;
        }

        if (RequestExportFolder is null)
        {
            StatusMessage = "Export folder picker is not available.";
            return;
        }

        var destinationDirectory = await RequestExportFolder();
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            StatusMessage = "Export cancelled.";
            return;
        }

        try
        {
            var outputPath = McpExportService.ExportToDirectory(selectedTools, destinationDirectory);
            StatusMessage = $"Exported {selectedTools.Count} tool(s) to {outputPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }
}

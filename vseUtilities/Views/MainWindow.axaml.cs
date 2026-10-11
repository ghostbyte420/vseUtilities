using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using vseUtilities.ViewModels;

namespace vseUtilities.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.RequestExportFolder = PickExportFolderAsync;
                viewModel.RequestCsprojFile = PickCsprojFileAsync;
            }
        };
    }

    /// <summary>
    /// Opens the OS-native folder picker so the user can choose where the
    /// merged .mcp.json should be written (e.g., next to their target
    /// project's .sln/.slnx file).
    /// </summary>
    private async Task<string?> PickExportFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the project folder to export .mcp.json into",
            AllowMultiple = false,
        });

        var folder = folders.FirstOrDefault();
        return folder?.TryGetLocalPath();
    }

    /// <summary>
    /// Opens the OS-native file picker, filtered to .csproj or built .exe
    /// files, so the user can manually add an MCP tool that "Add MCP" then
    /// places into the checklist (checkbox, name, modified date) just like
    /// any other tool.
    /// </summary>
    private async Task<string?> PickCsprojFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the .csproj or .exe for the MCP tool to add",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("MCP Tool (*.csproj, *.exe)")
                {
                    Patterns = new[] { "*.csproj", "*.exe" },
                },
            },
        });

        var file = files.FirstOrDefault();
        return file?.TryGetLocalPath();
    }
}

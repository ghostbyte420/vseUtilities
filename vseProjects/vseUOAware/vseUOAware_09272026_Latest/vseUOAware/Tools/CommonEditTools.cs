using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using vseUOAware.Services;

[McpServerToolType]
internal class CommonEditTools
{
    [McpServerTool]
    [Description("Adds, replaces or removes the uniform fill rule (HashKey = one terrain ID repeated 9 times) for ONE terrain in a COPY of Common.xml. Writes XMLFacetData\\Custom\\Transitions\\Common.xml plus INSTALL.md into a new/empty folder; the original is never touched. The terrain must exist in Terrain.xml. You supply the land tile IDs; nothing is guessed.")]
    public static string EditCommonXml(
        [Description("Full path to the current Common.xml.")] string commonXmlPath,
        [Description("Full path to Terrain.xml.")] string terrainXmlPath,
        [Description("Full path to a new/empty output folder.")] string outputFolderPath,
        [Description("Terrain ID 0-255 (decimal).")] int terrainId,
        [Description("add, replace or remove.")] string action,
        [Description("Comma list of LAND tile IDs (decimal or 0x hex). Ignored for remove.")] string mapTileIds = "",
        [Description("Optional comma list of STATIC tile IDs.")] string? staticTileIds = null,
        [Description("Optional description; defaults to 'Common - <terrain name>'.")] string? description = null,
        [Description("Optional tiledata.mul; adds '<name> (texture N)' comments.")] string? tileDataFilePath = null)
    {
        Func<int, string?>? comment = null;
        if (!string.IsNullOrWhiteSpace(tileDataFilePath) && File.Exists(tileDataFilePath))
        {
            var td = tileDataFilePath;
            comment = id =>
            {
                try
                {
                    var d = TileDataReader.GetLandDetail(td, id);
                    return d.Name + " (texture " + d.TextureId.ToString(CultureInfo.InvariantCulture) + ")";
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidDataException)
                {
                    return null;
                }
            };
        }
        var r = CommonEdit.Apply(commonXmlPath, terrainXmlPath, outputFolderPath, terrainId, action, mapTileIds, staticTileIds, description, comment);
        return (r.Ok ? "OK\n" : "FAILED\n") + r.Report;
    }
}

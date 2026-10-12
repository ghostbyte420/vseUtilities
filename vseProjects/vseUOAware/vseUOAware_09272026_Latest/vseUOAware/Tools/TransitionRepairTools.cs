using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using vseUOAware.Services;

[McpServerToolType]
internal class TransitionRepairTools
{
    [McpServerTool]
    [Description("Conservatively repairs MapCreator transition XML into a NEW output folder (originals never touched). Drops malformed/later-duplicate keys, trims out-of-range tile refs, reports unknown terrain IDs, and adds missing keys ONLY from the fills you supply. Writes XMLFacetData\\Custom\\Transitions plus REPORT.md and INSTALL.md. Output folder must be empty or new.")]
    public string RepairTransitionXml(
        [Description("Full path to the Transitions folder.")] string transitionsFolderPath,
        [Description("Full path to Terrain.xml.")] string terrainXmlPath,
        [Description("Full path to a new/empty output folder.")] string outputFolderPath,
        [Description("Optional fills, one per line: HashKey|Description|mapTileIds(comma)|staticIds(comma, optional). Never auto-picked.")] string? fills = null,
        [Description("Optional tiledata.mul; adds '<name> (texture N)' comments to written entries.")] string? tileDataFilePath = null)
    {
        Func<int, string?>? comment = null;
        if (!string.IsNullOrWhiteSpace(tileDataFilePath))
        {
            if (!File.Exists(tileDataFilePath)) return "tiledata.mul not found: " + tileDataFilePath;
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
        var lines = (fills ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var r = TransitionRepair.Run(transitionsFolderPath, terrainXmlPath, outputFolderPath, lines, comment);
        return (r.Ok ? "OK\n" : "FAILED\n") + r.Report;
    }
}

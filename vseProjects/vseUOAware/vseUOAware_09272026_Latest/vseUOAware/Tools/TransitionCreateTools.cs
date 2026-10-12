using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

[McpServerToolType]
internal class TransitionCreateTools
{
    [McpServerTool]
    [Description("Creates ONE new MapCreator transition rule (2-way, 3-way or more terrains in the 3x3 HashKey) as a NEW file under XMLFacetData\\Custom\\Transitions in a new/empty output folder, plus INSTALL.md. You supply the land tile IDs (any count) and optional static IDs; nothing is guessed. Refuses an existing HashKey, unknown terrain IDs, bad tile IDs and non-empty output folders. Originals never touched. HashKey = 18 hex chars = terrain IDs NW,N,NE,W,C,E,SW,S,SE.")]
    public static string CreateTransitionXml(
        [Description("Full path to the Transitions folder (used to refuse duplicate keys).")] string transitionsFolderPath,
        [Description("Full path to Terrain.xml (every terrain ID in the key must exist).")] string terrainXmlPath,
        [Description("Full path to a new/empty output folder.")] string outputFolderPath,
        [Description("18-hex-char HashKey, e.g. 010101010F0F0F0F0F.")] string hashKey,
        [Description("Description text stored in the rule.")] string description,
        [Description("Comma list of LAND tile IDs (decimal or 0x hex), e.g. '500,501,502'.")] string mapTileIds,
        [Description("New file name, e.g. 'Grass to Lava.xml'.")] string fileName,
        [Description("Optional comma list of STATIC tile IDs.")] string? staticTileIds = null,
        [Description("Optional tiledata.mul; adds '<name> (texture N)' comments for each land tile.")] string? tileDataFilePath = null)
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
                    return d.Name + " (texture " + d.TextureId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidDataException)
                {
                    return null;
                }
            };
        }
        var res = TransitionCreate.Create(transitionsFolderPath, terrainXmlPath, outputFolderPath, hashKey,
            description, mapTileIds, staticTileIds, fileName, comment);
        return (res.Ok ? "OK\n" : "FAILED\n") + res.Report;
    }
}

using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

[McpServerToolType]
internal class TerrainEditTools
{
    [McpServerTool]
    [Description("Adds ONE terrain type to a COPY of MapCreator's Terrain.xml (original never touched). Writes XMLFacetData\\Custom\\Terrain.xml plus INSTALL.md into a new/empty folder. Refuses duplicate IDs, duplicate colours, ID 254 (compiler hardcodes it) and bad TileIDs. Pass id=-1 to take the lowest free ID.")]
    public static string CreateTerrainType(
        [Description("Full path to the current Terrain.xml.")] string terrainXmlPath,
        [Description("Full path to a new/empty output folder.")] string outputFolderPath,
        [Description("Terrain display name.")] string name,
        [Description("Land TileID (0-16383) painted for this terrain.")] int tileId,
        [Description("Terrain.bmp palette colour: red 0-255.")] int r,
        [Description("Green 0-255.")] int g,
        [Description("Blue 0-255.")] int b,
        [Description("Terrain ID 0-255 (not 254), or -1 for the lowest free ID.")] int id = -1,
        [Description("Base altitude value (existing entries use 24, 88 or 216).")] int baseValue = 88,
        [Description("True if MapCreator should pick random tile variants.")] bool random = false)
    {
        var res = TerrainEdit.AddTerrain(terrainXmlPath, outputFolderPath, name, id, tileId, r, g, b, baseValue, random);
        return (res.Ok ? "OK\n" : "FAILED\n") + res.Report;
    }
}

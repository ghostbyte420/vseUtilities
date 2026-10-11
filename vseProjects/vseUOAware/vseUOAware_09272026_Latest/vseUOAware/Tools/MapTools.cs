using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Tools for inspecting a specific world coordinate on a facet: land tile/Z
/// and any static items placed there.
/// </summary>
internal class MapTools
{
    [McpServerTool]
    [Description("Inspects one UO world coordinate on a facet: land tile ID/Z and any static items placed there. Best-effort - relies on the map blocks being laid out in sequential block order.")]
    public string InspectMapCoordinate(
        [Description("World X coordinate.")] int x,
        [Description("World Y coordinate.")] int y,
        [Description("Facet (map) number: 0-5 are the defaults, any higher number is a custom map (Map6, Map32, Map10000...). If given, map/statics/index paths are filled in from the saved UO folder.")] int? facet = null,
        [Description("Full path to the facet's map file (map*LegacyMUL.uop or mapN.mul). Not needed when facet is given.")] string? mapUopFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks. Not needed for default facets 0-5. For custom maps give it (or facetWidthTiles) and the height is worked out from the file.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (= pixels in a paint program). Must be divisible by 8. Alternative to facetWidthBlocks.")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (= pixels). Must be divisible by 8. For custom maps give width and height (e.g. Trammel size 7168 x 4096); they are checked against the file.")] int facetHeightTiles = 0,
        [Description("Whether to also include static items at this tile.")] bool includeStatics = true)
    {
        if (!FacetResolver.TryTilesToBlocks(facetWidthTiles, ref facetWidthBlocks, out var tileErr))
            return tileErr;
        int hb = 0;
        if (!FacetResolver.TryTilesToBlocks(facetHeightTiles, ref hb, out tileErr))
            return tileErr;
        if (facet.HasValue)
        {
            if (!FacetResolver.TryResolve(facet.Value, null, facetWidthBlocks > 0 ? facetWidthBlocks : null, out var fp, out var fe, hb > 0 ? hb : null))
                return fe;
            mapUopFilePath = fp.MapFile;
            staticsMulFilePath = fp.StaticsFile;
            staIdxMulFilePath = fp.StaIdxFile;
            facetWidthBlocks = fp.WidthBlocks;
        }
        else if (facetWidthBlocks <= 0)
        {
            return "Give a facet number, or the width (facetWidthTiles or facetWidthBlocks) with the file paths. The width is never guessed.";
        }
        if (string.IsNullOrEmpty(mapUopFilePath) || string.IsNullOrEmpty(staticsMulFilePath) || string.IsNullOrEmpty(staIdxMulFilePath))
            return "Give a facet number, or all three file paths.";
        if (!File.Exists(mapUopFilePath))
            return $"File not found: {mapUopFilePath}";
        if (includeStatics && !File.Exists(staticsMulFilePath))
            return $"File not found: {staticsMulFilePath}";
        if (includeStatics && !File.Exists(staIdxMulFilePath))
            return $"File not found: {staIdxMulFilePath}";

        try
        {
            var info = MapCoordinateReader.GetCoordinate(mapUopFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, includeStatics);

            var sb = new StringBuilder();
            sb.AppendLine($"Coordinate ({info.X}, {info.Y}): land tile 0x{info.LandTileId:X4} ({info.LandTileId}), Z={info.LandZ}");

            if (includeStatics)
            {
                if (info.Statics.Count == 0)
                {
                    sb.AppendLine("No static items at this tile.");
                }
                else
                {
                    sb.AppendLine($"{info.Statics.Count} static item(s):");
                    foreach (var s in info.Statics)
                        sb.AppendLine($"  - tile 0x{s.TileId:X4} ({s.TileId}), Z={s.Z}, hue={s.Hue}");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to inspect coordinate ({x}, {y}): {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Reads a bounded rectangular coordinate region for land tiles/elevation and statics. Limited to 64x64 tiles per call to keep output manageable.")]
    public string InspectMapRegion(
        [Description("World X coordinate of the region's top-left corner.")] int x,
        [Description("World Y coordinate of the region's top-left corner.")] int y,
        [Description("Region width in tiles (max 64).")] int width,
        [Description("Region height in tiles (max 64).")] int height,
        [Description("Facet (map) number: 0-5 are the defaults, any higher number is a custom map (Map6, Map32, Map10000...). If given, map/statics/index paths are filled in from the saved UO folder.")] int? facet = null,
        [Description("Full path to the facet's map file (map*LegacyMUL.uop or mapN.mul). Not needed when facet is given.")] string? mapUopFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks. Not needed for default facets 0-5. For custom maps give it (or facetWidthTiles) and the height is worked out from the file.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (= pixels in a paint program). Must be divisible by 8. Alternative to facetWidthBlocks.")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (= pixels). Must be divisible by 8. For custom maps give width and height (e.g. Trammel size 7168 x 4096); they are checked against the file.")] int facetHeightTiles = 0,
        [Description("Whether to also include static items at each tile.")] bool includeStatics = true)
    {
        if (!FacetResolver.TryTilesToBlocks(facetWidthTiles, ref facetWidthBlocks, out var tileErr))
            return tileErr;
        int hb = 0;
        if (!FacetResolver.TryTilesToBlocks(facetHeightTiles, ref hb, out tileErr))
            return tileErr;
        if (facet.HasValue)
        {
            if (!FacetResolver.TryResolve(facet.Value, null, facetWidthBlocks > 0 ? facetWidthBlocks : null, out var fp, out var fe, hb > 0 ? hb : null))
                return fe;
            mapUopFilePath = fp.MapFile;
            staticsMulFilePath = fp.StaticsFile;
            staIdxMulFilePath = fp.StaIdxFile;
            facetWidthBlocks = fp.WidthBlocks;
        }
        else if (facetWidthBlocks <= 0)
        {
            return "Give a facet number, or the width (facetWidthTiles or facetWidthBlocks) with the file paths. The width is never guessed.";
        }
        if (string.IsNullOrEmpty(mapUopFilePath) || string.IsNullOrEmpty(staticsMulFilePath) || string.IsNullOrEmpty(staIdxMulFilePath))
            return "Give a facet number, or all three file paths.";
        if (!File.Exists(mapUopFilePath))
            return $"File not found: {mapUopFilePath}";
        if (includeStatics && !File.Exists(staticsMulFilePath))
            return $"File not found: {staticsMulFilePath}";
        if (includeStatics && !File.Exists(staIdxMulFilePath))
            return $"File not found: {staIdxMulFilePath}";

        if (width <= 0 || height <= 0)
            return "Width and height must both be positive.";
        if (width > 64 || height > 64)
            return "Width and height are each limited to 64 tiles per call to keep output manageable.";

        try
        {
            var region = MapCoordinateReader.GetRegion(mapUopFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, width, height, includeStatics);

            var sb = new StringBuilder();
            sb.AppendLine($"Region ({x}, {y}) to ({x + width - 1}, {y + height - 1}): {region.Count} tile(s).");

            foreach (var info in region)
            {
                sb.Append($"({info.X}, {info.Y}): land 0x{info.LandTileId:X4} ({info.LandTileId}), Z={info.LandZ}");

                if (includeStatics)
                {
                    if (info.Statics.Count == 0)
                    {
                        sb.AppendLine(" - no statics");
                    }
                    else
                    {
                        sb.AppendLine($" - {info.Statics.Count} static(s):");
                        foreach (var s in info.Statics)
                            sb.AppendLine($"    - tile 0x{s.TileId:X4} ({s.TileId}), Z={s.Z}, hue={s.Hue}");
                    }
                }
                else
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to inspect region ({x}, {y}, {width}x{height}): {ex.Message}";
        }
    }
}

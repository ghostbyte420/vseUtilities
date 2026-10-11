using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Read-only map analysis tools: tile usage search and coordinate conversion.
/// </summary>
internal class MapAnalysisTools
{
    private const int MaxArea = 512 * 512;
    private const int ChunkSize = 64;

    internal static bool Resolve(int? facet, string? map, string? statics, string? staIdx, int widthBlocks, int widthTiles, int heightTiles, bool needStatics,
        out string mapPath, out string staticsPath, out string staIdxPath, out int wb, out string error)
    {
        mapPath = map ?? ""; staticsPath = statics ?? ""; staIdxPath = staIdx ?? ""; wb = widthBlocks;
        if (!FacetResolver.TryTilesToBlocks(widthTiles, ref wb, out error)) return false;
        int hb = 0;
        if (!FacetResolver.TryTilesToBlocks(heightTiles, ref hb, out error)) return false;
        if (facet.HasValue)
        {
            if (!FacetResolver.TryResolve(facet.Value, null, wb > 0 ? wb : null, out var fp, out error, hb > 0 ? hb : null)) return false;
            mapPath = fp.MapFile; staticsPath = fp.StaticsFile; staIdxPath = fp.StaIdxFile; wb = fp.WidthBlocks;
        }
        else if (wb <= 0)
        {
            error = "Give a facet number, or the width (facetWidthTiles or facetWidthBlocks) with the file paths. The width is never guessed.";
            return false;
        }
        if (string.IsNullOrEmpty(mapPath) || (needStatics && (string.IsNullOrEmpty(staticsPath) || string.IsNullOrEmpty(staIdxPath))))
        {
            error = "Give a facet number, or all needed file paths.";
            return false;
        }
        if (!File.Exists(mapPath)) { error = $"File not found: {mapPath}"; return false; }
        if (needStatics && !File.Exists(staticsPath)) { error = $"File not found: {staticsPath}"; return false; }
        if (needStatics && !File.Exists(staIdxPath)) { error = $"File not found: {staIdxPath}"; return false; }
        error = "";
        return true;
    }

    [McpServerTool]
    [Description("Finds every place a land tile ID and/or static tile ID is used in a map region (max area 512x512). Gives count and coordinates. Read-only.")]
    public string FindTileUsage(
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles.")] int width,
        [Description("Region height in tiles.")] int height,
        [Description("Land tile ID to find (-1 = ignore).")] int landTileId = -1,
        [Description("Static tile ID to find (-1 = ignore).")] int staticTileId = -1,
        [Description("Max coordinates to list (default 100).")] int maxResults = 100,
        [Description("Facet number (0-5 defaults, higher = custom map).")] int? facet = null,
        [Description("Full path to the map file. Not needed when facet is given.")] string? mapUopFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8).")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8).")] int facetHeightTiles = 0)
    {
        if (landTileId < 0 && staticTileId < 0) return "Give landTileId and/or staticTileId.";
        if (width <= 0 || height <= 0 || (long)width * height > MaxArea) return "Region must be positive and at most 512x512 tiles.";
        if (!Resolve(facet, mapUopFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, staticTileId >= 0,
                out var map, out var sta, out var idx, out var wb, out var err))
            return err;
        try
        {
            int landCount = 0, staticCount = 0;
            var hits = new List<string>();
            for (int cy = y; cy < y + height; cy += ChunkSize)
                for (int cx = x; cx < x + width; cx += ChunkSize)
                {
                    int w = Math.Min(ChunkSize, x + width - cx), h = Math.Min(ChunkSize, y + height - cy);
                    foreach (var t in MapCoordinateReader.GetRegion(map, sta, idx, wb, cx, cy, w, h, staticTileId >= 0))
                    {
                        if (landTileId >= 0 && t.LandTileId == landTileId)
                        {
                            landCount++;
                            if (hits.Count < maxResults) hits.Add($"land ({t.X},{t.Y}) Z={t.LandZ}");
                        }
                        if (staticTileId >= 0)
                            foreach (var s in t.Statics)
                                if (s.TileId == staticTileId)
                                {
                                    staticCount++;
                                    if (hits.Count < maxResults) hits.Add($"static ({t.X},{t.Y}) Z={s.Z} hue={s.Hue}");
                                }
                    }
                }
            var sb = new StringBuilder();
            sb.AppendLine($"Region ({x},{y}) {width}x{height}: land matches={landCount}, static matches={staticCount}");
            foreach (var h in hits) sb.AppendLine("  " + h);
            if (landCount + staticCount > hits.Count) sb.AppendLine($"  ... {landCount + staticCount - hits.Count} more not listed");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to search tile usage: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Converts a tile coordinate to block, tile-in-block, paint-program pixel and flat-file byte offsets. Block index needs the facet height, so give a facet or width/height. Read-only.")]
    public string ConvertCoordinate(
        [Description("World X (tile = pixel).")] int x,
        [Description("World Y (tile = pixel).")] int y,
        [Description("Facet number (0-5 defaults, higher = custom map). Optional.")] int? facet = null,
        [Description("Full path to the map file (used to work out height). Optional.")] string? mapUopFilePath = null,
        [Description("Facet width in 8x8 blocks. Optional.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8). Optional.")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8). Optional.")] int facetHeightTiles = 0)
    {
        if (x < 0 || y < 0) return "X and Y must be 0 or more.";
        var sb = new StringBuilder();
        int bx = x / 8, by = y / 8, tx = x % 8, ty = y % 8;
        sb.AppendLine($"Tile ({x},{y}) = paint pixel ({x},{y}); block ({bx},{by}); tile in block ({tx},{ty}) = index {ty * 8 + tx}");
        if (facet.HasValue || !string.IsNullOrEmpty(mapUopFilePath) || facetWidthBlocks > 0 || facetWidthTiles > 0)
        {
            if (!Resolve(facet, mapUopFilePath, null, null, facetWidthBlocks, facetWidthTiles, facetHeightTiles, false,
                    out var map, out _, out _, out var wb, out var err))
                return err;
            try
            {
                int hb = MapCoordinateReader.ResolveHeightBlocks(map, wb);
                if (bx >= wb || by >= hb) return $"Outside the facet ({wb * 8} x {hb * 8} tiles).";
                long block = (long)bx * hb + by;
                sb.AppendLine($"Facet {wb * 8} x {hb * 8} tiles ({wb} x {hb} blocks). Block index = {block} (column-major).");
                sb.AppendLine($"Flat .mul byte offset: block start {block * 196}, this tile {block * 196 + 4 + (ty * 8 + tx) * 3}");
                sb.AppendLine("UOP files: offset is inside the decompressed entry, not the file.");
            }
            catch (Exception ex)
            {
                return $"Failed to convert: {ex.Message}";
            }
        }
        return sb.ToString();
    }
}

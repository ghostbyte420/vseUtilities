using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Read-only comparison of two map files (land tile, Z and optionally statics) over a region.
/// </summary>
internal class MapDiffTools
{
    private const int MaxArea = 512 * 512;
    private const int ChunkSize = 64;

    [McpServerTool]
    [Description("Compares two maps (flat mul or UOP, same size) over a region (max 512x512): counts and lists tiles whose land tile ID, land Z or statics differ. Both maps are given by explicit file paths. Read-only.")]
    public string DiffMaps(
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles.")] int width,
        [Description("Region height in tiles.")] int height,
        [Description("Full path to map A (mapN.mul or mapNLegacyMUL.uop).")] string mapAFilePath,
        [Description("Full path to map B.")] string mapBFilePath,
        [Description("Facet width in tiles (divisible by 8), the same for both maps.")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8). Optional; checked against the files.")] int facetHeightTiles = 0,
        [Description("Facet width in 8x8 blocks. Alternative to facetWidthTiles.")] int facetWidthBlocks = 0,
        [Description("Also compare statics (needs the four statics/staidx paths).")] bool compareStatics = false,
        [Description("Full path to staticsN.mul for map A.")] string? staticsAFilePath = null,
        [Description("Full path to staidxN.mul for map A.")] string? staIdxAFilePath = null,
        [Description("Full path to staticsN.mul for map B.")] string? staticsBFilePath = null,
        [Description("Full path to staidxN.mul for map B.")] string? staIdxBFilePath = null,
        [Description("Max differing tiles to list (default 50).")] int maxResults = 50)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaxArea) return "Region must be positive and at most 512x512 tiles.";
        if (!MapAnalysisTools.Resolve(null, mapAFilePath, staticsAFilePath, staIdxAFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, compareStatics,
                out var mapA, out var staA, out var idxA, out var wbA, out var err))
            return "Map A: " + err;
        if (!MapAnalysisTools.Resolve(null, mapBFilePath, staticsBFilePath, staIdxBFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, compareStatics,
                out var mapB, out var staB, out var idxB, out var wbB, out err))
            return "Map B: " + err;
        try
        {
            int hbA = MapCoordinateReader.ResolveHeightBlocks(mapA, wbA);
            int hbB = MapCoordinateReader.ResolveHeightBlocks(mapB, wbB);
            if (hbA != hbB) return $"The maps differ in size: A is {wbA * 8} x {hbA * 8} tiles, B is {wbB * 8} x {hbB * 8} tiles.";

            long tilesCompared = 0, landIdDiff = 0, zDiff = 0, staticDiff = 0, tilesDiff = 0;
            var lines = new List<string>();
            for (int cy = y; cy < y + height; cy += ChunkSize)
                for (int cx = x; cx < x + width; cx += ChunkSize)
                {
                    int w = Math.Min(ChunkSize, x + width - cx), h = Math.Min(ChunkSize, y + height - cy);
                    var a = MapCoordinateReader.GetRegion(mapA, staA, idxA, wbA, cx, cy, w, h, compareStatics);
                    var b = MapCoordinateReader.GetRegion(mapB, staB, idxB, wbB, cx, cy, w, h, compareStatics);
                    if (a.Count != b.Count) return $"Internal mismatch: region returned {a.Count} tiles for A and {b.Count} for B.";
                    for (int i = 0; i < a.Count; i++)
                    {
                        tilesCompared++;
                        var ta = a[i]; var tb = b[i];
                        var parts = new List<string>();
                        if (ta.LandTileId != tb.LandTileId) { landIdDiff++; parts.Add($"land 0x{ta.LandTileId:X4}->0x{tb.LandTileId:X4}"); }
                        if (ta.LandZ != tb.LandZ) { zDiff++; parts.Add($"Z {ta.LandZ}->{tb.LandZ}"); }
                        if (compareStatics && StaticsKey(ta) != StaticsKey(tb))
                        {
                            staticDiff++;
                            parts.Add($"statics {ta.Statics.Count}->{tb.Statics.Count}");
                        }
                        if (parts.Count == 0) continue;
                        tilesDiff++;
                        if (lines.Count < maxResults) lines.Add($"({ta.X},{ta.Y}): {string.Join(", ", parts)}");
                    }
                }
            var sb = new StringBuilder();
            sb.AppendLine($"Compared {tilesCompared} tiles in ({x},{y}) {width}x{height}: {tilesDiff} differ (land tile ID {landIdDiff}, land Z {zDiff}{(compareStatics ? $", statics {staticDiff}" : ", statics not compared")}).");
            foreach (var l in lines) sb.AppendLine("  " + l);
            if (tilesDiff > lines.Count) sb.AppendLine($"  ... {tilesDiff - lines.Count} more not listed");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to diff maps: {ex.Message}";
        }
    }

    private static string StaticsKey(MapCoordinateInfo t) =>
        string.Join(";", t.Statics.OrderBy(s => s.TileId).ThenBy(s => s.Z).ThenBy(s => s.Hue).Select(s => $"{s.TileId}:{s.Z}:{s.Hue}"));
}

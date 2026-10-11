using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Read-only checks of statics in a map region: problem finder and a simple walkability check.
/// </summary>
internal class MapStaticsTools
{
    private const int MaxArea = 512 * 512;
    private const int ChunkSize = 64;
    private const int BuriedThreshold = 10;
    private const int StandHeight = 16;

    [McpServerTool]
    [Description("Finds static problems in a map region (max 512x512): exact duplicate statics (same ID/X/Y/Z), statics buried 10+ Z below the land, and static IDs with no tiledata entry. Read-only.")]
    public string FindStaticProblems(
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles.")] int width,
        [Description("Region height in tiles.")] int height,
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Facet number (0-5 defaults, higher = custom map).")] int? facet = null,
        [Description("Full path to the map file. Not needed when facet is given.")] string? mapFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8).")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8).")] int facetHeightTiles = 0,
        [Description("Max problems to list (default 50).")] int maxResults = 50)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaxArea) return "Region must be positive and at most 512x512 tiles.";
        if (!File.Exists(tileDataFilePath)) return "tiledata.mul not found: " + tileDataFilePath;
        if (!MapAnalysisTools.Resolve(facet, mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, true,
                out var map, out var sta, out var idx, out var wb, out var err))
            return err;
        try
        {
            var known = new HashSet<int>(TileDataReader.ReadAll(tileDataFilePath).Where(t => t.IsStaticItem).Select(t => t.TileId));
            long tiles = 0, statics = 0, dup = 0, buried = 0, unknown = 0;
            var lines = new List<string>();
            void Add(string s) { if (lines.Count < maxResults) lines.Add(s); }
            foreach (var t in Chunks(map, sta, idx, wb, x, y, width, height))
            {
                tiles++;
                var seen = new HashSet<(int, int)>();
                foreach (var s in t.Statics)
                {
                    statics++;
                    if (!seen.Add((s.TileId, s.Z))) { dup++; Add($"({t.X},{t.Y}): duplicate static 0x{s.TileId:X4} at Z {s.Z}"); }
                    if (s.Z <= t.LandZ - BuriedThreshold) { buried++; Add($"({t.X},{t.Y}): static 0x{s.TileId:X4} Z {s.Z} buried under land Z {t.LandZ}"); }
                    if (!known.Contains(0x4000 + s.TileId)) { unknown++; Add($"({t.X},{t.Y}): static 0x{s.TileId:X4} has no tiledata entry"); }
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine($"Checked {tiles} tiles, {statics} statics in ({x},{y}) {width}x{height}: {dup} duplicates, {buried} buried, {unknown} unknown IDs.");
            foreach (var l in lines) sb.AppendLine("  " + l);
            long total = dup + buried + unknown;
            if (total > lines.Count) sb.AppendLine($"  ... {total - lines.Count} more not listed");
            return sb.ToString();
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    [McpServerTool]
    [Description("Simple walkability check for a map region (max 512x512). A tile is blocked if its land is Impassable or Wet (water), or a static within 16 Z above the land is Impassable and not a Surface. Heuristic, not the server's exact movement rules. Read-only.")]
    public string CheckWalkability(
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles.")] int width,
        [Description("Region height in tiles.")] int height,
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Facet number (0-5 defaults, higher = custom map).")] int? facet = null,
        [Description("Full path to the map file. Not needed when facet is given.")] string? mapFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8).")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8).")] int facetHeightTiles = 0,
        [Description("Max blocked tiles to list (default 30).")] int maxResults = 30)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaxArea) return "Region must be positive and at most 512x512 tiles.";
        if (!File.Exists(tileDataFilePath)) return "tiledata.mul not found: " + tileDataFilePath;
        if (!MapAnalysisTools.Resolve(facet, mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, true,
                out var map, out var sta, out var idx, out var wb, out var err))
            return err;
        try
        {
            var landCache = new Dictionary<int, TileFlag>();
            var itemCache = new Dictionary<int, TileFlag>();
            TileFlag Land(int id) { if (!landCache.TryGetValue(id, out var f)) landCache[id] = f = TileDataReader.GetLandDetail(tileDataFilePath, id).Flags; return f; }
            TileFlag Item(int id) { if (!itemCache.TryGetValue(id, out var f)) itemCache[id] = f = TileDataReader.GetItemDetail(tileDataFilePath, 0x4000 + id).Flags; return f; }

            long tiles = 0, walk = 0, water = 0, landBlocked = 0, staticBlocked = 0;
            var lines = new List<string>();
            foreach (var t in Chunks(map, sta, idx, wb, x, y, width, height))
            {
                tiles++;
                var lf = Land(t.LandTileId);
                string? why = null;
                if (lf.HasFlag(TileFlag.Wet)) { water++; why = "water"; }
                else if (lf.HasFlag(TileFlag.Impassable)) { landBlocked++; why = "impassable land"; }
                else
                {
                    foreach (var s in t.Statics)
                    {
                        if (s.Z < t.LandZ || s.Z > t.LandZ + StandHeight) continue;
                        var sf = Item(s.TileId);
                        if (sf.HasFlag(TileFlag.Impassable) && !sf.HasFlag(TileFlag.Surface)) { staticBlocked++; why = $"static 0x{s.TileId:X4} at Z {s.Z}"; break; }
                    }
                }
                if (why == null) walk++;
                else if (lines.Count < maxResults) lines.Add($"({t.X},{t.Y}): blocked by {why}");
            }
            var sb = new StringBuilder();
            sb.AppendLine($"{tiles} tiles in ({x},{y}) {width}x{height}: {walk} walkable, {water} water, {landBlocked} impassable land, {staticBlocked} blocked by statics.");
            foreach (var l in lines) sb.AppendLine("  " + l);
            long blocked = tiles - walk;
            if (blocked > lines.Count) sb.AppendLine($"  ... {blocked - lines.Count} more not listed");
            return sb.ToString();
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    private static IEnumerable<MapCoordinateInfo> Chunks(string map, string sta, string idx, int wb, int x, int y, int width, int height)
    {
        for (int cy = y; cy < y + height; cy += ChunkSize)
            for (int cx = x; cx < x + width; cx += ChunkSize)
            {
                int w = Math.Min(ChunkSize, x + width - cx), h = Math.Min(ChunkSize, y + height - cy);
                foreach (var t in MapCoordinateReader.GetRegion(map, sta, idx, wb, cx, cy, w, h, true)) yield return t;
            }
    }
}

using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using vseUOAware.Services;

/// <summary>
/// Read-only placement checks: a coordinate list validator and a house footprint check.
/// </summary>
internal class MapPlacementTools
{
    private const int MaxCoordinates = 500;
    private const int MaxFootprint = 64;
    private const int StandHeight = 16;
    private static readonly Regex NumberPair = new(@"(-?\d+)\s*[, ]\s*(-?\d+)", RegexOptions.Compiled);

    [McpServerTool]
    [Description("Validates a list of coordinates (one 'x,y' or 'x y' per line, or separated by ';', max 500) against a map: out of bounds, water, impassable land, or blocked by a static. Read-only.")]
    public string ValidateCoordinateList(
        [Description("Coordinates, e.g. '1432,1696' on each line or separated by ';'.")] string coordinates,
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Facet number (0-5 defaults, higher = custom map).")] int? facet = null,
        [Description("Full path to the map file. Not needed when facet is given.")] string? mapFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8).")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8).")] int facetHeightTiles = 0)
    {
        if (string.IsNullOrWhiteSpace(coordinates)) return "No coordinates given.";
        if (!File.Exists(tileDataFilePath)) return "tiledata.mul not found: " + tileDataFilePath;
        var points = NumberPair.Matches(coordinates).Select(m => (X: int.Parse(m.Groups[1].Value), Y: int.Parse(m.Groups[2].Value))).ToList();
        if (points.Count == 0) return "No 'x,y' pairs found in the text.";
        if (points.Count > MaxCoordinates) return $"Too many coordinates ({points.Count}); the maximum is {MaxCoordinates}.";
        if (!MapAnalysisTools.Resolve(facet, mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, true,
                out var map, out var sta, out var idx, out var wb, out var err))
            return err;
        try
        {
            int widthTiles = wb * 8, heightTiles = MapCoordinateReader.ResolveHeightBlocks(map, wb) * 8;
            int ok = 0;
            var lines = new List<string>();
            foreach (var (px, py) in points)
            {
                string? problem = CheckTile(map, sta, idx, wb, widthTiles, heightTiles, tileDataFilePath, px, py);
                if (problem == null) ok++;
                else lines.Add($"({px},{py}): {problem}");
            }
            var sb = new StringBuilder();
            sb.AppendLine($"{points.Count} coordinates checked on a {widthTiles}x{heightTiles} map: {ok} ok, {points.Count - ok} with problems.");
            foreach (var l in lines) sb.AppendLine("  " + l);
            return sb.ToString();
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    [McpServerTool]
    [Description("Checks whether a house footprint (rectangle, max 64x64 tiles) can be placed: every tile must be on the map, not water, not impassable land, not blocked by an impassable static, and the land Z spread must be within maxZSpread. Heuristic, not the server's exact house rules. Read-only.")]
    public string CheckHousePlacement(
        [Description("Footprint top-left X.")] int x,
        [Description("Footprint top-left Y.")] int y,
        [Description("Footprint width in tiles.")] int width,
        [Description("Footprint height in tiles.")] int height,
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Facet number (0-5 defaults, higher = custom map).")] int? facet = null,
        [Description("Full path to the map file. Not needed when facet is given.")] string? mapFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8).")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8).")] int facetHeightTiles = 0,
        [Description("Largest allowed difference between the highest and lowest land Z (default 5).")] int maxZSpread = 5,
        [Description("Max problem tiles to list (default 20).")] int maxResults = 20)
    {
        if (width <= 0 || height <= 0 || width > MaxFootprint || height > MaxFootprint) return $"Footprint must be positive and at most {MaxFootprint}x{MaxFootprint} tiles.";
        if (!File.Exists(tileDataFilePath)) return "tiledata.mul not found: " + tileDataFilePath;
        if (!MapAnalysisTools.Resolve(facet, mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, true,
                out var map, out var sta, out var idx, out var wb, out var err))
            return err;
        try
        {
            int widthTiles = wb * 8, heightTiles = MapCoordinateReader.ResolveHeightBlocks(map, wb) * 8;
            if (x < 0 || y < 0 || x + width > widthTiles || y + height > heightTiles)
                return $"Footprint ({x},{y}) {width}x{height} is outside the {widthTiles}x{heightTiles} map.";

            var tiles = MapCoordinateReader.GetRegion(map, sta, idx, wb, x, y, width, height, true);
            int minZ = int.MaxValue, maxZ = int.MinValue, bad = 0;
            var lines = new List<string>();
            foreach (var t in tiles)
            {
                minZ = Math.Min(minZ, t.LandZ); maxZ = Math.Max(maxZ, t.LandZ);
                string? problem = Blocked(t, tileDataFilePath);
                if (problem == null) continue;
                bad++;
                if (lines.Count < maxResults) lines.Add($"({t.X},{t.Y}): {problem}");
            }
            int spread = maxZ - minZ;
            bool zOk = spread <= maxZSpread;
            var sb = new StringBuilder();
            sb.AppendLine($"Footprint ({x},{y}) {width}x{height}: {(bad == 0 && zOk ? "CAN be placed" : "CANNOT be placed")}. {bad} blocked tiles, land Z {minZ}..{maxZ} (spread {spread}, allowed {maxZSpread}).");
            foreach (var l in lines) sb.AppendLine("  " + l);
            if (bad > lines.Count) sb.AppendLine($"  ... {bad - lines.Count} more not listed");
            return sb.ToString();
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    private static string? CheckTile(string map, string sta, string idx, int wb, int widthTiles, int heightTiles, string tileData, int x, int y)
    {
        if (x < 0 || y < 0 || x >= widthTiles || y >= heightTiles) return "outside the map";
        var t = MapCoordinateReader.GetRegion(map, sta, idx, wb, x, y, 1, 1, true)[0];
        return Blocked(t, tileData);
    }

    private static string? Blocked(MapCoordinateInfo t, string tileData)
    {
        var land = TileDataReader.GetLandDetail(tileData, t.LandTileId).Flags;
        if (land.HasFlag(TileFlag.Wet)) return "water";
        if (land.HasFlag(TileFlag.Impassable)) return $"impassable land 0x{t.LandTileId:X4}";
        foreach (var s in t.Statics)
        {
            if (s.Z < t.LandZ || s.Z > t.LandZ + StandHeight) continue;
            var f = TileDataReader.GetItemDetail(tileData, 0x4000 + s.TileId).Flags;
            if (f.HasFlag(TileFlag.Impassable) && !f.HasFlag(TileFlag.Surface)) return $"blocked by static 0x{s.TileId:X4} at Z {s.Z}";
        }
        return null;
    }
}

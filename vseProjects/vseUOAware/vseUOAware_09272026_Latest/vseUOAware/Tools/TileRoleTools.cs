using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Reads the user-owned TileRoles.txt so tools never guess what an art ID is for.
/// Read-only: never writes to map, statics, tiledata or art files.
/// </summary>
internal class TileRoleTools
{
    private sealed record RoleRule(bool IsStatic, int From, int To, string Role, string Note);

    private static string ResolveRoleFile(string? roleFilePath)
    {
        if (!string.IsNullOrWhiteSpace(roleFilePath)) return roleFilePath;
        return Path.Combine(AppContext.BaseDirectory, "TileRoles.txt");
    }

    private static (List<RoleRule> Rules, string? Error) Load(string? roleFilePath)
    {
        string path = ResolveRoleFile(roleFilePath);
        if (!File.Exists(path)) return ([], $"Role file not found: {path}");
        var rules = new List<RoleRule>();
        int lineNo = 0;
        foreach (var raw in File.ReadAllLines(path))
        {
            lineNo++;
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var p = line.Split('|', StringSplitOptions.TrimEntries);
            if (p.Length < 3) return ([], $"{path} line {lineNo}: expected 'kind | ids | role | note'.");
            bool isStatic = p[0].Equals("static", StringComparison.OrdinalIgnoreCase);
            if (!isStatic && !p[0].Equals("land", StringComparison.OrdinalIgnoreCase))
                return ([], $"{path} line {lineNo}: kind must be 'land' or 'static'.");
            var range = p[1].Split('-', 2, StringSplitOptions.TrimEntries);
            if (!TryNum(range[0], out int from) || !TryNum(range[^1], out int to))
                return ([], $"{path} line {lineNo}: bad id or range '{p[1]}'.");
            rules.Add(new RoleRule(isStatic, from, to, p[2], p.Length > 3 ? p[3] : string.Empty));
        }

        return (rules, null);
    }

    private static bool TryNum(string s, out int v) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)
            : int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

    private static string? RoleOf(List<RoleRule> rules, bool isStatic, int id)
    {
        string? role = null;
        foreach (var r in rules)
            if (r.IsStatic == isStatic && id >= r.From && id <= r.To) role = r.Role;
        return role;
    }

    [McpServerTool]
    [Description("Lists the tile roles defined in the user-owned TileRoles.txt (what each land/static ID range is for). Read-only.")]
    public string ListTileRoles(
        [Description("Optional full path to TileRoles.txt (default: next to the server executable).")] string? roleFilePath = null)
    {
        var (rules, err) = Load(roleFilePath);
        if (err is not null) return err;
        var sb = new StringBuilder($"{rules.Count} role rule(s) from {ResolveRoleFile(roleFilePath)}:\n");
        foreach (var r in rules)
            sb.AppendLine($"  {(r.IsStatic ? "static" : "land  ")} {r.From}-{r.To}  {r.Role}  {r.Note}");
        return sb.ToString();
    }

    [McpServerTool]
    [Description("Counts every land and static ID in a map region by its role from TileRoles.txt, and lists IDs that are NOT in the role file (marking Wet ones) so nothing is guessed. Never judges Z differences. Read-only.")]
    public string SurveyTileRoles(
        [Description("Full path to mapN.mul or mapNLegacyMUL.uop.")] string mapFilePath,
        [Description("Full path to staticsN.mul.")] string staticsMulFilePath,
        [Description("Full path to staidxN.mul.")] string staIdxMulFilePath,
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Facet width in 8x8 blocks (181 for map4).")] int facetWidthBlocks,
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles (max area 512x512).")] int width,
        [Description("Region height in tiles (max area 512x512).")] int height,
        [Description("Optional full path to TileRoles.txt.")] string? roleFilePath = null)
    {
        if (!File.Exists(mapFilePath) || !File.Exists(tileDataFilePath)) return "Map file or tiledata.mul not found.";
        if (width < 1 || height < 1 || (long)width * height > 512 * 512) return "Region too large or empty: max area is 262144 tiles.";
        var (rules, err) = Load(roleFilePath);
        if (err is not null) return err;

        var cells = MapCoordinateReader.GetRegion(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, width, height, includeStatics: true);
        var roleCounts = new SortedDictionary<string, int>();
        var unlistedLand = new SortedDictionary<int, int>();
        var unlistedStatic = new SortedDictionary<int, int>();
        foreach (var c in cells)
        {
            string? lr = RoleOf(rules, false, c.LandTileId);
            if (lr is null) unlistedLand[c.LandTileId] = unlistedLand.GetValueOrDefault(c.LandTileId) + 1;
            else roleCounts["land:" + lr] = roleCounts.GetValueOrDefault("land:" + lr) + 1;
            foreach (var s in c.Statics)
            {
                string? sr = RoleOf(rules, true, s.TileId);
                if (sr is null) unlistedStatic[s.TileId] = unlistedStatic.GetValueOrDefault(s.TileId) + 1;
                else roleCounts["static:" + sr] = roleCounts.GetValueOrDefault("static:" + sr) + 1;
            }
        }

        var sb = new StringBuilder($"Region ({x},{y}) {width}x{height}, roles from {ResolveRoleFile(roleFilePath)}\nRole counts:\n");
        foreach (var kv in roleCounts) sb.AppendLine($"  {kv.Key} x{kv.Value}");
        sb.AppendLine("Land IDs not in the role file (top 25 by count; * = Wet in tiledata):");
        foreach (var kv in unlistedLand.OrderByDescending(k => k.Value).Take(25))
        {
            var d = TileDataReader.GetLandDetail(tileDataFilePath, kv.Key & 0x3FFF);
            sb.AppendLine($"  {kv.Key} '{d.Name}' x{kv.Value}{(d.Flags.HasFlag(TileFlag.Wet) ? " *" : "")}");
        }

        sb.AppendLine("Static IDs not in the role file (top 25 by count; * = Wet in tiledata):");
        foreach (var kv in unlistedStatic.OrderByDescending(k => k.Value).Take(25))
        {
            var d = TileDataReader.GetItemDetail(tileDataFilePath, 0x4000 + kv.Key);
            sb.AppendLine($"  {kv.Key} '{d.Name}' x{kv.Value}{(d.Flags.HasFlag(TileFlag.Wet) ? " *" : "")}");
        }

        return sb.ToString();
    }

    [McpServerTool]
    [Description("Describes the pixel shape of one art tile from art.mul/artidx.mul: bounding box, how much of its box is filled, left/right/top/bottom fill, and the role from TileRoles.txt. A hint to help tell caps from full tiles; the role file stays the authority. Read-only.")]
    public string DescribeTileShape(
        [Description("Full path to artidx.mul.")] string artIdxFilePath,
        [Description("Full path to art.mul.")] string artMulFilePath,
        [Description("Tile ID as stored in the map (land 0-16383, or static ID).")] int tileId,
        [Description("True if tileId is a static/item ID (the 0x4000 offset is added for you).")] bool isStatic,
        [Description("Optional full path to TileRoles.txt.")] string? roleFilePath = null)
    {
        var (rules, err) = Load(roleFilePath);
        if (err is not null) return err;
        var img = ArtReader.GetArtTileMul(artIdxFilePath, artMulFilePath, isStatic ? 0x4000 + tileId : tileId);
        string role = RoleOf(rules, isStatic, tileId) ?? "(not in role file)";
        if (img is null) return $"{(isStatic ? "static" : "land")} {tileId}: no art. Role: {role}";

        int minX = img.Width, minY = img.Height, maxX = -1, maxY = -1, filled = 0;
        long r = 0, g = 0, b = 0;
        for (int py = 0; py < img.Height; py++)
            for (int px = 0; px < img.Width; px++)
            {
                int i = (py * img.Width + px) * 4;
                if (img.Rgba[i + 3] == 0) continue;
                filled++;
                r += img.Rgba[i]; g += img.Rgba[i + 1]; b += img.Rgba[i + 2];
                minX = Math.Min(minX, px); maxX = Math.Max(maxX, px);
                minY = Math.Min(minY, py); maxY = Math.Max(maxY, py);
            }

        if (filled == 0) return $"{(isStatic ? "static" : "land")} {tileId}: art is fully transparent. Role: {role}";
        int bw = maxX - minX + 1, bh = maxY - minY + 1;
        double fill = 100.0 * filled / (bw * bh);
        double Half(Func<int, int, bool> inHalf)
        {
            int n = 0, f = 0;
            for (int py = minY; py <= maxY; py++)
                for (int px = minX; px <= maxX; px++)
                {
                    if (!inHalf(px, py)) continue;
                    n++;
                    if (img.Rgba[(py * img.Width + px) * 4 + 3] != 0) f++;
                }

            return n == 0 ? 0 : 100.0 * f / n;
        }

        int midX = (minX + maxX) / 2, midY = (minY + maxY) / 2;
        return $"{(isStatic ? "static" : "land")} {tileId}: art {img.Width}x{img.Height}, box {bw}x{bh}, filled {filled} px ({fill:F0}% of box)\n" +
               $"  half fill: left {Half((px, _) => px <= midX):F0}%, right {Half((px, _) => px > midX):F0}%, top {Half((_, py) => py <= midY):F0}%, bottom {Half((_, py) => py > midY):F0}%\n" +
               $"  average color RGB({r / filled},{g / filled},{b / filled})\n  Role: {role}";
    }
}

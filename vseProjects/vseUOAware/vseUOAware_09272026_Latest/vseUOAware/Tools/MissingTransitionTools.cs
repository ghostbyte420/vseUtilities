using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Xml;
using vseUOAware.Services;

/// <summary>
/// Read-only MapCreator source checks. Mirrors CompileYourNewMap: key = 9 group IDs (NW,N,NE,W,C,E,SW,S,SE) as 2-digit uppercase hex,
/// edges wrap, and a missing rule silently falls back to the plain terrain tile.
/// </summary>
[McpServerToolType]
internal class MissingTransitionTools
{
    private const string Hex = "0123456789ABCDEF";

    [McpServerTool]
    [Description("Finds 3x3 terrain patterns in a MapCreator Terrain.bmp (8-bit indexed) that have NO matching HashKey in the Transitions XML folder, so MapCreator would silently use the plain terrain tile there. All-same-group patterns are ignored (normal). Edges wrap like MapCreator. Read-only; lists each missing key with cell count and first coordinate.")]
    public string FindMissingTransitions(
        [Description("Full path to Terrain.bmp (8-bit indexed).")] string terrainBmpPath,
        [Description("Full path to the Transitions folder (searched recursively for *.xml).")] string transitionsFolderPath,
        [Description("Max missing keys to list, most frequent first (default 30).")] int maxResults = 30,
        [Description("Comma list of terrain group IDs (decimal) treated as the official UO black 'Blank' side; a 3x3 pattern made only of these IDs plus at most one other group is skipped. Default '19'. Pass empty to report everything.")] string ignoreGroupIds = "19")
    {
        var ignored = new bool[256];
        foreach (var part in (ignoreGroupIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out int gid) || gid < 0 || gid > 255) return "Bad ignoreGroupIds value: " + part;
            ignored[gid] = true;
        }
        if (!File.Exists(terrainBmpPath)) return "Terrain.bmp not found: " + terrainBmpPath;
        if (!Directory.Exists(transitionsFolderPath)) return "Transitions folder not found: " + transitionsFolderPath;
        if (!BmpIndexReader.TryRead(terrainBmpPath, out var px, out int w, out int h, out var err)) return "Cannot read Terrain.bmp: " + err;

        var rules = new HashSet<string>(StringComparer.Ordinal);
        int files = 0, bad = 0;
        foreach (var f in Directory.EnumerateFiles(transitionsFolderPath, "*.xml", SearchOption.AllDirectories))
        {
            files++;
            try
            {
                var doc = new XmlDocument();
                doc.Load(f);
                foreach (XmlElement e in doc.GetElementsByTagName("TransInfo"))
                    rules.Add(e.GetAttribute("HashKey").Trim().ToUpperInvariant());
            }
            catch { bad++; }
        }

        var missing = new Dictionary<string, (long Count, int X, int Y)>();
        long nonUniform = 0, skippedIgnored = 0;
        var cells = new byte[9];
        var chars = new char[18];
        for (int y = 0; y < h; y++)
        {
            int yu = y == 0 ? h - 1 : y - 1, yd = y == h - 1 ? 0 : y + 1;
            for (int x = 0; x < w; x++)
            {
                int xl = x == 0 ? w - 1 : x - 1, xr = x == w - 1 ? 0 : x + 1;
                cells[0] = px[yu * w + xl]; cells[1] = px[yu * w + x]; cells[2] = px[yu * w + xr];
                cells[3] = px[y * w + xl]; cells[4] = px[y * w + x]; cells[5] = px[y * w + xr];
                cells[6] = px[yd * w + xl]; cells[7] = px[yd * w + x]; cells[8] = px[yd * w + xr];
                bool uniform = true;
                for (int k = 1; k < 9; k++) if (cells[k] != cells[0]) { uniform = false; break; }
                if (uniform) continue;
                nonUniform++;
                int otherGroups = 0, firstOther = -1;
                bool hasIgnored = false;
                for (int k = 0; k < 9; k++)
                {
                    if (ignored[cells[k]]) { hasIgnored = true; continue; }
                    if (firstOther < 0) { firstOther = cells[k]; otherGroups = 1; }
                    else if (cells[k] != firstOther) otherGroups = 2;
                }
                if (hasIgnored && otherGroups <= 1) { skippedIgnored++; continue; }
                for (int k = 0; k < 9; k++) { chars[k * 2] = Hex[cells[k] >> 4]; chars[k * 2 + 1] = Hex[cells[k] & 15]; }
                string key = new string(chars);
                if (rules.Contains(key)) continue;
                missing[key] = missing.TryGetValue(key, out var m) ? (m.Count + 1, m.X, m.Y) : (1, x, y);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Terrain.bmp {w}x{h}; {files} XML files ({bad} unreadable), {rules.Count} distinct HashKeys.");
        sb.AppendLine($"Non-uniform 3x3 cells: {nonUniform:N0} ({skippedIgnored:N0} skipped as ignored Blank edges); distinct missing patterns: {missing.Count:N0}; cells hit: {missing.Values.Sum(v => v.Count):N0}.");
        foreach (var kv in missing.OrderByDescending(k => k.Value.Count).Take(Math.Max(1, maxResults)))
        {
            string k = kv.Key;
            sb.AppendLine($"{k}  x{kv.Value.Count:N0}  first at ({kv.Value.X},{kv.Value.Y})  grid: {k[..6]} / {k.Substring(6, 6)} / {k[12..]}");
        }
        if (missing.Count == 0) sb.AppendLine("No missing transitions.");
        return sb.ToString();
    }
}

using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Xml.Linq;

/// <summary>
/// Read-only helpers for MapCreator XML facet data (Terrain.xml and Transitions/*.xml).
/// A HashKey is 18 hex characters = nine 2-character terrain IDs (hex), in the order
/// MapCreator reads them (position 0..8 of a 3x3 neighbourhood).
/// </summary>
internal class MapCreatorXmlTools
{
    private static Dictionary<int, string> LoadTerrains(string terrainXmlPath)
    {
        var map = new Dictionary<int, string>();
        foreach (var t in XDocument.Load(terrainXmlPath).Descendants("Terrain"))
        {
            if (int.TryParse((string?)t.Attribute("ID"), out int id))
            {
                map[id] = (string?)t.Attribute("Name") ?? "?";
            }
        }

        return map;
    }

    private static bool TryDecode(string key, out int[] ids)
    {
        ids = new int[9];
        if (key.Length != 18)
        {
            return false;
        }

        for (int i = 0; i < 9; i++)
        {
            if (!int.TryParse(key.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out ids[i]))
            {
                return false;
            }
        }

        return true;
    }

    [McpServerTool]
    [Description("Runs the transition XML check on EVERY .xml file under a Transitions folder (recursive) and returns one summary line per file plus the first few issues of each file that has any. Read-only.")]
    public string ValidateTransitionFolder(
        [Description("Full path to MapCreator's Terrain.xml.")] string terrainXmlPath,
        [Description("Full path to the Transitions folder.")] string transitionsFolderPath,
        [Description("Max issues to show per file (default 3).")] int issuesPerFile = 3)
    {
        if (!Directory.Exists(transitionsFolderPath))
        {
            return $"Folder not found: {transitionsFolderPath}";
        }

        var files = Directory.EnumerateFiles(transitionsFolderPath, "*.xml", SearchOption.AllDirectories).OrderBy(f => f).ToList();
        var sb = new StringBuilder();
        int clean = 0, dirty = 0, broken = 0;
        foreach (var file in files)
        {
            string rel = Path.GetRelativePath(transitionsFolderPath, file);
            try
            {
                string result = ValidateTransitionXml(terrainXmlPath, file, issuesPerFile);
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (lines[0].Contains(" 0 issue(s)"))
                {
                    clean++;
                }
                else
                {
                    dirty++;
                    sb.AppendLine($"[{rel}] {lines[0]}");
                    foreach (var l in lines.Skip(1))
                    {
                        sb.AppendLine("    " + l);
                    }
                }
            }
            catch (Exception ex)
            {
                broken++;
                sb.AppendLine($"[{rel}] UNREADABLE: {ex.Message}");
            }
        }

        return $"Checked {files.Count} files: {clean} clean, {dirty} with issues, {broken} unreadable.\n{sb}";
    }

    [McpServerTool]
    [Description("Decodes a MapCreator transition HashKey: 18 hex chars = nine 2-char hex terrain IDs, shown with Terrain.xml names as a 3x3 grid (positions 0-8). Read-only.")]
    public string DecodeTransitionHashKey(
        [Description("Full path to MapCreator's Terrain.xml.")] string terrainXmlPath,
        [Description("The 18-character HashKey, e.g. 282828282828282828.")] string hashKey)
    {
        if (!File.Exists(terrainXmlPath))
        {
            return $"Terrain.xml not found: {terrainXmlPath}";
        }

        hashKey = hashKey.Trim();
        if (!TryDecode(hashKey, out var ids))
        {
            return $"Not a valid HashKey: need exactly 18 hex characters (9 x 2), got {hashKey.Length} chars '{hashKey}'.";
        }

        var names = LoadTerrains(terrainXmlPath);
        var sb = new StringBuilder();
        sb.AppendLine($"HashKey {hashKey} (positions 0-8, left-to-right then down):");
        for (int i = 0; i < 9; i++)
        {
            string name = names.TryGetValue(ids[i], out var n) ? n : "UNKNOWN (not in Terrain.xml)";
            sb.AppendLine($"  [{i}] hex {ids[i]:X2} = ID {ids[i]} = {name}");
        }

        int distinct = ids.Distinct().Count();
        sb.AppendLine(distinct == 1
            ? "All nine positions are the same terrain: a plain fill rule, not a border."
            : $"{distinct} different terrains: this is a transition/border rule.");
        return sb.ToString();
    }

    [McpServerTool]
    [Description("Checks one MapCreator Transitions XML file for problems: bad/short HashKeys, terrain IDs missing from Terrain.xml, duplicate HashKeys, rules with no MapTile art IDs, and non-numeric TileIDs. Read-only; reports up to maxIssues.")]
    public string ValidateTransitionXml(
        [Description("Full path to MapCreator's Terrain.xml.")] string terrainXmlPath,
        [Description("Full path to a Transitions XML file, e.g. Common.xml.")] string transitionXmlPath,
        [Description("Maximum issues to list (default 50).")] int maxIssues = 50)
    {
        if (!File.Exists(terrainXmlPath))
        {
            return $"Terrain.xml not found: {terrainXmlPath}";
        }

        if (!File.Exists(transitionXmlPath))
        {
            return $"Transition XML not found: {transitionXmlPath}";
        }

        var terrains = LoadTerrains(terrainXmlPath);
        var issues = new List<string>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int rules = 0;

        foreach (var info in XDocument.Load(transitionXmlPath).Descendants("TransInfo"))
        {
            rules++;
            string desc = (string?)info.Attribute("Description") ?? "(no description)";
            string key = ((string?)info.Attribute("HashKey") ?? string.Empty).Trim();

            if (!TryDecode(key, out var ids))
            {
                issues.Add($"BAD KEY   '{desc}': HashKey '{key}' is not 18 hex characters.");
            }
            else
            {
                foreach (int id in ids.Distinct().Where(i => !terrains.ContainsKey(i)))
                {
                    issues.Add($"UNKNOWN ID '{desc}': terrain ID {id} (hex {id:X2}) is not in Terrain.xml.");
                }
            }

            if (key.Length > 0)
            {
                if (seen.TryGetValue(key, out var first))
                {
                    issues.Add($"DUPLICATE '{desc}' repeats HashKey {key} already used by '{first}'.");
                }
                else
                {
                    seen[key] = desc;
                }
            }

            var tiles = info.Descendants("MapTile").ToList();
            var statics = info.Descendants("StaticTile").ToList();
            bool hasFile = info.Attribute("File") is not null;
            if (tiles.Count == 0 && statics.Count == 0 && !hasFile)
            {
                issues.Add($"EMPTY     '{desc}': no MapTile, StaticTile or File art defined.");
            }

            foreach (var t in tiles.Concat(statics))
            {
                string? v = (string?)t.Attribute("TileID") ?? (string?)t.Attribute("MapTile") ?? (string?)t.Attribute("StaticTile");
                if (!int.TryParse(v, out _))
                {
                    issues.Add($"BAD TILE  '{desc}': {t.Name.LocalName} has missing/non-numeric art ID '{v}'.");
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{Path.GetFileName(transitionXmlPath)}: {rules} rules, {issues.Count} issue(s).");
        foreach (var line in issues.Take(Math.Max(1, maxIssues)))
        {
            sb.AppendLine(line);
        }

        if (issues.Count > maxIssues)
        {
            sb.AppendLine($"... {issues.Count - maxIssues} more not shown.");
        }

        return sb.ToString();
    }
}

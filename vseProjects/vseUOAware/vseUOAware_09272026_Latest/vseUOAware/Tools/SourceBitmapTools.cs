using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Xml;
using vseUOAware.Services;

/// <summary>Read-only checks of MapCreator source bitmaps (Terrain.bmp / Altitude.bmp).</summary>
[McpServerToolType]
internal class SourceBitmapTools
{
    [McpServerTool]
    [Description("Checks a MapCreator Terrain.bmp (and optionally Altitude.bmp): 8-bit indexed, width/height divisible by 8, same size as Altitude.bmp, and every Terrain.bmp palette index exists as a GroupID in Terrain.xml. Read-only; lists unknown indices with pixel counts and first coordinate.")]
    public static string CheckSourceBitmaps(
        [Description("Full path to Terrain.bmp (8-bit indexed).")] string terrainBmpPath,
        [Description("Full path to MapCreator's Terrain.xml.")] string terrainXmlPath,
        [Description("Optional full path to Altitude.bmp (8-bit indexed); must match Terrain.bmp size.")] string? altitudeBmpPath = null,
        [Description("Max unknown indices to list (default 20).")] int maxResults = 20)
    {
        if (!File.Exists(terrainBmpPath)) return "Terrain.bmp not found: " + terrainBmpPath;
        if (!File.Exists(terrainXmlPath)) return "Terrain.xml not found: " + terrainXmlPath;

        var known = new bool[256];
        try
        {
            var doc = new XmlDocument();
            doc.Load(terrainXmlPath);
            foreach (XmlNode n in doc.SelectNodes("//Terrain")!)
            {
                if (int.TryParse(n.Attributes?["ID"]?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && id is >= 0 and <= 255) known[id] = true;
            }
        }
        catch (XmlException ex) { return "Terrain.xml is not valid XML: " + ex.Message; }

        if (!BmpIndexReader.TryRead(terrainBmpPath, out var idx, out int w, out int h, out string err)) return "Terrain.bmp problem: " + err;

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Terrain.bmp {w}x{h}.");
        if (w % 8 != 0 || h % 8 != 0) sb.AppendLine("PROBLEM: width and height must be divisible by 8 (map blocks are 8x8).");

        if (!string.IsNullOrWhiteSpace(altitudeBmpPath))
        {
            if (!File.Exists(altitudeBmpPath)) sb.AppendLine("PROBLEM: Altitude.bmp not found: " + altitudeBmpPath);
            else if (!BmpIndexReader.TryRead(altitudeBmpPath, out _, out int aw, out int ah, out string aerr)) sb.AppendLine("PROBLEM: Altitude.bmp: " + aerr);
            else if (aw != w || ah != h) sb.AppendLine(CultureInfo.InvariantCulture, $"PROBLEM: Altitude.bmp is {aw}x{ah} but Terrain.bmp is {w}x{h}; sizes must match.");
            else sb.AppendLine(CultureInfo.InvariantCulture, $"Altitude.bmp {aw}x{ah}: size matches.");
        }

        var count = new long[256];
        var first = new int[256];
        Array.Fill(first, -1);
        for (int i = 0; i < idx.Length; i++)
        {
            byte v = idx[i];
            count[v]++;
            if (first[v] < 0) first[v] = i;
        }

        var unknown = Enumerable.Range(0, 256).Where(v => count[v] > 0 && !known[v]).OrderByDescending(v => count[v]).ToList();
        if (unknown.Count == 0) sb.AppendLine("All Terrain.bmp palette indices exist in Terrain.xml.");
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"PROBLEM: {unknown.Count} palette index(es) used in Terrain.bmp are NOT in Terrain.xml:");
            foreach (int v in unknown.Take(Math.Max(1, maxResults)))
                sb.AppendLine(CultureInfo.InvariantCulture, $"  index {v}  x{count[v]}  first at ({first[v] % w},{first[v] / w})");
        }
        return sb.ToString();
    }
}

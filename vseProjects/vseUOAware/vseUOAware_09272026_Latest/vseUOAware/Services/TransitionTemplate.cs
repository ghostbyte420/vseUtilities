using System.Text;
using System.Xml.Linq;

namespace vseUOAware.Services;

/// <summary>Writes a fill-in-the-blanks transition XML: every tile is a comment saying which direction it goes.</summary>
internal static class TransitionTemplate
{
    // Cell order in a HashKey: NW,N,NE,W,C,E,SW,S,SE.
    internal static readonly (string Name, string Where, int[] Cells)[] Shapes =
    {
        ("Edge North", "the OTHER terrain is along the TOP (north) edge", new[] { 0, 1, 2 }),
        ("Edge South", "the OTHER terrain is along the BOTTOM (south) edge", new[] { 6, 7, 8 }),
        ("Edge West", "the OTHER terrain is along the LEFT (west) edge", new[] { 0, 3, 6 }),
        ("Edge East", "the OTHER terrain is along the RIGHT (east) edge", new[] { 2, 5, 8 }),
        ("Corner NW", "the OTHER terrain touches only the TOP-LEFT (north-west) corner", new[] { 0 }),
        ("Corner NE", "the OTHER terrain touches only the TOP-RIGHT (north-east) corner", new[] { 2 }),
        ("Corner SW", "the OTHER terrain touches only the BOTTOM-LEFT (south-west) corner", new[] { 6 }),
        ("Corner SE", "the OTHER terrain touches only the BOTTOM-RIGHT (south-east) corner", new[] { 8 }),
        ("Big Corner NW", "the OTHER terrain wraps the TOP and LEFT sides (north-west)", new[] { 0, 1, 2, 3, 6 }),
        ("Big Corner NE", "the OTHER terrain wraps the TOP and RIGHT sides (north-east)", new[] { 0, 1, 2, 5, 8 }),
        ("Big Corner SW", "the OTHER terrain wraps the BOTTOM and LEFT sides (south-west)", new[] { 0, 3, 6, 7, 8 }),
        ("Big Corner SE", "the OTHER terrain wraps the BOTTOM and RIGHT sides (south-east)", new[] { 2, 5, 6, 7, 8 }),
    };

    internal static string Run(string terrainXmlPath, int centerTerrainId, int otherTerrainId, string outputFolderPath)
    {
        if (!File.Exists(terrainXmlPath)) return "Terrain.xml not found: " + terrainXmlPath;
        if (Directory.Exists(outputFolderPath) && Directory.EnumerateFileSystemEntries(outputFolderPath).Any())
            return "Output folder is not empty. Use a new or empty folder: " + outputFolderPath;
        var names = new Dictionary<int, string>();
        foreach (var t in XDocument.Load(terrainXmlPath).Descendants("Terrain"))
            if (int.TryParse((string?)t.Attribute("ID"), out var id)) names[id] = (string?)t.Attribute("Name") ?? "?";
        if (!names.TryGetValue(centerTerrainId, out var a)) return $"Terrain ID {centerTerrainId} is not in Terrain.xml.";
        if (!names.TryGetValue(otherTerrainId, out var b)) return $"Terrain ID {otherTerrainId} is not in Terrain.xml.";

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<!--");
        sb.AppendLine($"  TEMPLATE: {a} (ID {centerTerrainId}) in the middle, {b} (ID {otherTerrainId}) around it.");
        sb.AppendLine("  HOW TO USE: replace every TileID=\"REPLACE_ME\" with a real tile ID (a number).");
        sb.AppendLine("  Each comment tells you which way that tile must face. Delete a rule you do not need.");
        sb.AppendLine("  The 3x3 key reads left to right, top to bottom: NW N NE / W C E / SW S SE.");
        sb.AppendLine("-->");
        sb.AppendLine("<Trans>");
        foreach (var (name, where, cells) in Shapes)
        {
            var key = new StringBuilder();
            for (var i = 0; i < 9; i++) key.Append((cells.Contains(i) ? otherTerrainId : centerTerrainId).ToString("X2"));
            sb.AppendLine($"  <!-- {name}: {where}. -->");
            sb.AppendLine($"  <TransInfo Description=\"{a} To {b} - {name}\" HashKey=\"{key}\">");
            sb.AppendLine("    <MapTiles>");
            sb.AppendLine($"      <!-- LAND TILE HERE: art for \"{name}\" ({where}) -->");
            sb.AppendLine("      <MapTile TileID=\"REPLACE_ME\" AltIDMod=\"0\" />");
            sb.AppendLine("    </MapTiles>");
            sb.AppendLine("    <StaticTiles>");
            sb.AppendLine($"      <!-- STATIC TILE HERE (optional): a static on the \"{name}\" side. Delete this line if none. -->");
            sb.AppendLine("      <StaticTile TileID=\"REPLACE_ME\" />");
            sb.AppendLine("    </StaticTiles>");
            sb.AppendLine("  </TransInfo>");
        }
        sb.AppendLine("</Trans>");

        var dir = Path.Combine(outputFolderPath, "XMLFacetData", "Custom", "Transitions");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{a} to {b} TEMPLATE.xml");
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputFolderPath, "INSTALL.md"),
            "# Template only\nThis file is NOT ready to use. Fill in every REPLACE_ME with a real tile ID, then run validate_transition_xml before copying it into MapCreator.\n");
        return $"Wrote template with {Shapes.Length} rules:\n{file}\nFill in each REPLACE_ME using the comments, then validate it. Originals were not touched.";
    }
}

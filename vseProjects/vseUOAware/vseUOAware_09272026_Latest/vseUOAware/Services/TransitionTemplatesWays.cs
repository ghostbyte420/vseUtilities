using System.Text;
using System.Xml.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace vseUOAware.Services;

internal static class TransitionTemplatesWays
{
    [ThreadStatic] private static List<(int[] Ids, int[] Order)>? _rules;

    private static void WriteDiagrams(string outFolder, int[] terrainOrder)
    {
        var dir = Path.Combine(outFolder, "diagrams");
        Directory.CreateDirectory(dir);
        Rgba32[] colors = { new(80, 170, 80, 255), new(255, 20, 200, 255), new(60, 120, 255, 255) };
        var n = 0;
        foreach (var (ids, order) in _rules ?? new())
        {
            n++;
            const int cell = 60;
            using var img = new Image<Rgba32>(cell * 3, cell * 3);
            for (var c = 0; c < 9; c++)
            {
                var col = colors[Math.Max(0, Array.IndexOf(order, ids[c]))];
                for (var y = 0; y < cell; y++)
                    for (var x = 0; x < cell; x++)
                    {
                        var edge = x < 2 || y < 2 || x >= cell - 2 || y >= cell - 2;
                        var mid = c == 4 && (x < 6 || y < 6 || x >= cell - 6 || y >= cell - 6);
                        img[(c % 3) * cell + x, (c / 3) * cell + y] = mid ? new Rgba32(255, 255, 255, 255) : edge ? new Rgba32(0, 0, 0, 255) : col;
                    }
            }
            img.SaveAsPng(Path.Combine(dir, $"rule_{n:00}.png"));
        }
        File.WriteAllText(Path.Combine(dir, "README.txt"),
            "rule_NN.png = the 3x3 picture of rule NN. GREEN = A (middle ground). PINK = B. BLUE = C. White frame = the [ ] square.\r\n" +
            "Top of each picture is MAP NORTH. ins*/outs* PNGs are the original reference diagrams (copied, unchanged).\r\n");
        foreach (var sub in new[] { "ins", "outs" })
        {
            var src = Path.Combine(AppContext.BaseDirectory, "Diagrams", sub);
            if (Directory.Exists(src))
                foreach (var f in Directory.GetFiles(src, "*.png"))
                    File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true);
        }
    }

    private static readonly (string Name, string Kind, string Screen, int[] Cells)[] Shapes2 =
    {
        ("Edge North", "FLAT EDGE", "a straight edge at the TOP-RIGHT", new[] { 0, 1, 2 }),
        ("Edge East", "FLAT EDGE", "a straight edge at the BOTTOM-RIGHT", new[] { 2, 5, 8 }),
        ("Edge South", "FLAT EDGE", "a straight edge at the BOTTOM-LEFT", new[] { 6, 7, 8 }),
        ("Edge West", "FLAT EDGE", "a straight edge at the TOP-LEFT", new[] { 0, 3, 6 }),
        ("Corner NW", "SMALL CORNER", "one small bit at the TOP point", new[] { 0 }),
        ("Corner NE", "SMALL CORNER", "one small bit at the RIGHT point", new[] { 2 }),
        ("Corner SE", "SMALL CORNER", "one small bit at the BOTTOM point", new[] { 8 }),
        ("Corner SW", "SMALL CORNER", "one small bit at the LEFT point", new[] { 6 }),
        ("Big Corner NW", "BIG CORNER", "an L-shape along the TOP-LEFT and TOP-RIGHT edges", new[] { 0, 1, 2, 3, 6 }),
        ("Big Corner NE", "BIG CORNER", "an L-shape along the TOP-RIGHT and BOTTOM-RIGHT edges", new[] { 0, 1, 2, 5, 8 }),
        ("Big Corner SE", "BIG CORNER", "an L-shape along the BOTTOM-RIGHT and BOTTOM-LEFT edges", new[] { 2, 5, 6, 7, 8 }),
        ("Big Corner SW", "BIG CORNER", "an L-shape along the BOTTOM-LEFT and TOP-LEFT edges", new[] { 0, 3, 6, 7, 8 }),
    };

    private static readonly Dictionary<string, string> RealNames = new()
    {
        ["Edge South"] = "NS", ["Edge North"] = "SN", ["Edge East"] = "WE", ["Edge West"] = "EW",
        ["Big Corner SE"] = "NWA", ["Big Corner NW"] = "SEA", ["Big Corner SW"] = "NEA", ["Big Corner NE"] = "SWA",
        ["Corner NW"] = "NWB", ["Corner SE"] = "SEB", ["Corner NE"] = "NEB", ["Corner SW"] = "SWB",
    };

    private static readonly (string Name, string Screen, int[] Cells)[] Sides =
    {
        ("North", "TOP-RIGHT edge", new[] { 0, 1, 2 }),
        ("East", "BOTTOM-RIGHT edge", new[] { 2, 5, 8 }),
        ("South", "BOTTOM-LEFT edge", new[] { 6, 7, 8 }),
        ("West", "TOP-LEFT edge", new[] { 0, 3, 6 }),
    };

    private static Dictionary<int, string>? LoadNames(string path)
    {
        if (!File.Exists(path)) return null;
        var names = new Dictionary<int, string>();
        foreach (var t in XDocument.Load(path).Descendants("Terrain"))
            if (int.TryParse((string?)t.Attribute("ID"), out var id)) names[id] = (string?)t.Attribute("Name") ?? "?";
        return names;
    }

    private static string? Check(string terrainXml, string outFolder, int[] ids, out Dictionary<int, string> names)
    {
        names = new Dictionary<int, string>();
        var n = LoadNames(terrainXml);
        if (n == null) return "Terrain.xml not found: " + terrainXml;
        if (Directory.Exists(outFolder) && Directory.EnumerateFileSystemEntries(outFolder).Any())
            return "Output folder is not empty. Use a new or empty folder: " + outFolder;
        foreach (var id in ids) if (!n.ContainsKey(id)) return $"Terrain ID {id} is not in Terrain.xml.";
        if (ids.Distinct().Count() != ids.Length) return "Terrain IDs must all be different.";
        names = n;
        return null;
    }

    private static void Intro(StringBuilder sb, string title, string legend)
    {
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<!--");
        sb.AppendLine("  " + title);
        sb.AppendLine("");
        sb.AppendLine("  THE LETTERS (they mean the same thing in the whole file):");
        sb.AppendLine(legend);
        sb.AppendLine("");
        sb.AppendLine("  WHAT YOU DO, ONE RULE AT A TIME:");
        sb.AppendLine("  1. Look at the little 3x3 picture. It is 9 squares of ground.");
        sb.AppendLine("  2. The letter in [ ] is the ONE square you are making a tile for.");
        sb.AppendLine("  3. Find a land tile (in Fiddler) that looks like the description under the picture.");
        sb.AppendLine("  4. Type that tile's number where it says REPLACE_ME in the MapTile line.");
        sb.AppendLine("  5. The StaticTile line is optional (an extra item on top). Do not want one? Delete that line.");
        sb.AppendLine("  6. Do not need a rule? Delete the whole rule.");
        sb.AppendLine("");
        sb.AppendLine("  ONE THING TO KNOW: the map is turned on your screen.");
        sb.AppendLine("  The top of the picture is MAP NORTH. On your screen, MAP NORTH is the TOP-RIGHT.");
        sb.AppendLine("  Every rule tells you where things are on your SCREEN, so you can ignore map directions.");
        sb.AppendLine("-->");
        sb.AppendLine("<Trans>");
        _rules = new();
    }

    private static void Rule(StringBuilder sb, int number, int total, string desc, string title, int[] ids, Dictionary<int, string> letter, string[] lines)
    {
        string L(int i) => letter[ids[i]];
        _rules?.Add((ids, letter.OrderBy(k => k.Value).Select(k => k.Key).ToArray()));
        sb.AppendLine("  <!--");
        sb.AppendLine("  ==================================================");
        sb.AppendLine($"  RULE {number} of {total}: {title}");
        sb.AppendLine("  ==================================================");
        sb.AppendLine($"        {L(0)}   {L(1)}   {L(2)}");
        sb.AppendLine($"        {L(3)}  [{L(4)}]  {L(5)}");
        sb.AppendLine($"        {L(6)}   {L(7)}   {L(8)}");
        sb.AppendLine("");
        foreach (var line in lines) sb.AppendLine("  " + line);
        sb.AppendLine("");
        sb.AppendLine("  THE TILE IS A DIAMOND. Go around it. Each part must be:");
        string P(int cell) => letter[ids[cell]];
        sb.AppendLine($"    TOP point:         {P(0)}");
        sb.AppendLine($"    TOP-RIGHT edge:    {P(1)}");
        sb.AppendLine($"    RIGHT point:       {P(2)}");
        sb.AppendLine($"    BOTTOM-RIGHT edge: {P(5)}");
        sb.AppendLine($"    BOTTOM point:      {P(8)}");
        sb.AppendLine($"    BOTTOM-LEFT edge:  {P(7)}");
        sb.AppendLine($"    LEFT point:        {P(6)}");
        sb.AppendLine($"    TOP-LEFT edge:     {P(3)}");
        sb.AppendLine($"    CENTER of tile:    {P(4)}   (this is the [ ] square)");
        sb.AppendLine("  (Use the A/B/C letters from the top of the file.)");
        sb.AppendLine("  Put the tile number in the MapTile line below.");
        sb.AppendLine("  -->");
        sb.AppendLine($"  <TransInfo Description=\"{desc}\" HashKey=\"{string.Concat(ids.Select(i => i.ToString("X2")))}\">");
        sb.AppendLine("    <MapTiles>");
        sb.AppendLine("      <MapTile TileID=\"REPLACE_ME\" AltIDMod=\"0\" />");
        sb.AppendLine("    </MapTiles>");
        sb.AppendLine("    <StaticTiles>");
        sb.AppendLine("      <StaticTile TileID=\"REPLACE_ME\" />");
        sb.AppendLine("    </StaticTiles>");
        sb.AppendLine("  </TransInfo>");
        sb.AppendLine("");
    }

    private static string Write(string outFolder, string fileName, StringBuilder sb, int count)
    {
        sb.AppendLine("</Trans>");
        var dir = Path.Combine(outFolder, "XMLFacetData", "Custom", "Transitions");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, fileName);
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        WriteDiagrams(outFolder, Array.Empty<int>());
        File.WriteAllText(Path.Combine(outFolder, "INSTALL.md"),
            "# Template only\nNOT ready to use. Fill in every REPLACE_ME, then run validate_transition_xml before copying into MapCreator.\n");
        var diagrams = Path.Combine(outFolder, "diagrams");
        var text = new StringBuilder();
        var num = 0;
        foreach (var (rids, order) in _rules ?? new())
        {
            num++;
            string Row(int r) => string.Concat(Enumerable.Range(r * 3, 3).Select(c => "ABC"[Math.Max(0, Array.IndexOf(order, rids[c]))]));
            text.AppendLine($"rule {num:00}: {Row(0)} / {Row(1)} / {Row(2)}  HashKey {string.Concat(rids.Select(x => x.ToString("X2")))}  picture: rule_{num:00}.png");
        }
        var rulesTxt = Path.Combine(diagrams, "rules.txt");
        Directory.CreateDirectory(diagrams);
        File.WriteAllText(rulesTxt, "TEXT VERSION OF THE PICTURES. Rows are top / middle / bottom of the 3x3 (top = MAP NORTH). Letters are A/B/C from the top of the XML. Middle letter of the middle row is the square being made.\r\n" + text);
        return $"Wrote template with {count} rules:\n{file}\nDiagrams (pink = B, one PNG per rule):\n{diagrams}\nText version of every picture:\n{rulesTxt}\nInstall note:\n{Path.Combine(outFolder, "INSTALL.md")}\n\nRULES (3x3 as top / middle / bottom):\n{text}\nNEXT: replace every REPLACE_ME with a land tile ID, then run validate_transition_xml. Originals were not touched.";
    }

    internal static string RunTwoWay(string terrainXml, int terrainA, int terrainB, string outFolder)
    {
        var err = Check(terrainXml, outFolder, new[] { terrainA, terrainB }, out var names);
        if (err != null) return err;
        string a = names[terrainA], b = names[terrainB];
        var letter = new Dictionary<int, string> { [terrainA] = "A", [terrainB] = "B" };
        var sb = new StringBuilder();
        Intro(sb, $"2-WAY TEMPLATE: {a} meets {b}. There are 12 rules, all for {a} squares (A is always the middle square).",
            $"    A = {a}\n    B = {b}");
        var n = 0;
        foreach (var (mid, other, mn, on) in new[] { (terrainA, terrainB, a, b) })
            foreach (var (name, kind, screen, cells) in Shapes2)
            {
                n++;
                var ids = Enumerable.Range(0, 9).Select(i => cells.Contains(i) ? other : mid).ToArray();
                var ml = letter[mid];
                var ol = letter[other];
                Rule(sb, n, 12, $"{mn} to {on} - {name}", $"{kind}  ({mn} square with {on} next to it)  [MapCreator drawer name: {RealNames[name]}]", ids, letter, new[]
                {
                    $"THE SQUARE IN [ ] IS {mn.ToUpperInvariant()} ({ml}).",
                    $"{on.ToUpperInvariant()} ({ol}) is next to it: {screen}.",
                    $"FIND A TILE THAT IS: mostly {mn}, with {on} showing: {screen}.",
                });
            }
        return Write(outFolder, $"{a} 2-way {b} TEMPLATE.xml", sb, n);
    }

    internal static string RunThreeWay(string terrainXml, int terrainA, int terrainB, int terrainC, string outFolder)
    {
        var err = Check(terrainXml, outFolder, new[] { terrainA, terrainB, terrainC }, out var names);
        if (err != null) return err;
        string a = names[terrainA], b = names[terrainB], c = names[terrainC];
        var letter = new Dictionary<int, string> { [terrainA] = "A", [terrainB] = "B", [terrainC] = "C" };
        var sb = new StringBuilder();
        Intro(sb, $"3-WAY TEMPLATE: {a} is in the middle. {b} and {c} are both next to it. There are 12 rules.",
            $"    A = {a}   (this is the ground in the middle [ ])\n    B = {b}\n    C = {c}\n" +
            $"  If two edges touch at a corner square, that corner square is B ({b}).\n" +
            $"  Want {b} or {c} in the middle instead? Run the tool again and put that ground first (as A).");
        var n = 0;
        for (var i = 0; i < 4; i++)
            foreach (var j in new[] { (i + 2) % 4, (i + 1) % 4, (i + 3) % 4 })
            {
                n++;
                var side1 = Sides[i];
                var side2 = Sides[j];
                var ids = Enumerable.Range(0, 9).Select(k => side1.Cells.Contains(k) ? terrainB
                    : side2.Cells.Contains(k) ? terrainC : terrainA).ToArray();
                var across = j == (i + 2) % 4;
                var kind = across ? "TWO EDGES ACROSS FROM EACH OTHER" : "TWO EDGES NEXT TO EACH OTHER (an L-shape)";
                Rule(sb, n, 12, $"{a} with {b} {side1.Name} and {c} {side2.Name}", kind, ids, letter, new[]
                {
                    $"THE SQUARE IN [ ] IS {a.ToUpperInvariant()} (A).",
                    $"{b.ToUpperInvariant()} (B) is on the {side1.Screen}.",
                    $"{c.ToUpperInvariant()} (C) is on the {side2.Screen}.",
                    $"FIND A TILE THAT IS: mostly {a}, with {b} along the {side1.Screen} and {c} along the {side2.Screen}.",
                });
            }
        return Write(outFolder, $"{a} 3-way {b} {c} TEMPLATE.xml", sb, n);
    }
}

using System.Globalization;
using System.Xml.Linq;

namespace vseUOAware.Services;

internal sealed record TerrainEditResult(bool Ok, string Report, int NewId);

/// <summary>
/// Adds ONE terrain type to a copy of Terrain.xml. The original is never touched; the copy is written to
/// a new/empty folder as XMLFacetData\Custom\Terrain.xml plus INSTALL.md. The result must pass MapCreatorXml.ValidateTerrains.
/// </summary>
internal static class TerrainEdit
{
    public const string CustomFile = "XMLFacetData\\Custom\\Terrain.xml";

    /// <summary>id = -1 picks the lowest free ID (never 254).</summary>
    public static TerrainEditResult AddTerrain(string terrainXmlPath, string outputFolder, string name, int id,
        int tileId, int r, int g, int b, int baseValue, bool random)
    {
        if (!File.Exists(terrainXmlPath)) return Fail("Terrain.xml not found: " + terrainXmlPath);
        if (string.IsNullOrWhiteSpace(name)) return Fail("Name is required.");
        if (tileId < 0 || tileId > MapCreatorXml.MaxLandTileId) return Fail($"TileID {tileId} outside 0-{MapCreatorXml.MaxLandTileId}.");
        if (Directory.Exists(outputFolder) && Directory.EnumerateFileSystemEntries(outputFolder).Any())
            return Fail("Output folder is not empty; refusing to write: " + outputFolder);

        var existing = MapCreatorXml.LoadTerrains(terrainXmlPath);
        if (id < 0) id = NextFreeId(existing);
        if (id < 0) return Fail("No free terrain ID left in 0-255.");

        var doc = XDocument.Load(terrainXmlPath);
        var root = doc.Root;
        if (root == null) return Fail("Terrain.xml has no root element.");
        root.Add(new XElement("Terrain",
            new XAttribute("Name", name), new XAttribute("ID", Num(id)), new XAttribute("TileID", Num(tileId)),
            new XAttribute("R", Num(r)), new XAttribute("G", Num(g)), new XAttribute("B", Num(b)),
            new XAttribute("Base", Num(baseValue)), new XAttribute("Random", random ? "True" : "False")));

        var after = new List<TerrainDef>(existing) { new(name, id, tileId, r, g, b, baseValue, random) };
        var before = new HashSet<string>(MapCreatorXml.ValidateTerrains(existing));
        var problems = MapCreatorXml.ValidateTerrains(after).Where(p => !before.Contains(p)).ToList();
        if (problems.Count > 0) return Fail("Not written. New terrain would cause:\n- " + string.Join("\n- ", problems));

        var target = Path.Combine(outputFolder, CustomFile);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        doc.Save(target);
        File.WriteAllText(Path.Combine(outputFolder, "INSTALL.md"),
            "# Install\n\nOriginal Terrain.xml was not modified.\n\nCopy `XMLFacetData\\Custom\\Terrain.xml` into MapCreator.Engine\\Compiler\\XMLFacetData\\Custom\\ " +
            "(embedded recursively by the csproj, no project edit needed), then rebuild MapCreator.Engine.\n\n" +
            $"Added: ID {id} '{name}' TileID {tileId} colour {r},{g},{b} Base {baseValue} Random {random}.\n" +
            "Remember: transitions for the new ID need their own HashKeys or MapCreator falls back to the plain tile.\n");
        return new TerrainEditResult(true, $"Added terrain ID {id} '{name}'. Wrote {target} and INSTALL.md. Originals untouched.", id);
    }

    public static int NextFreeId(IEnumerable<TerrainDef> terrains)
    {
        var used = terrains.Select(t => t.Id).ToHashSet();
        for (int i = 0; i <= 255; i++)
            if (i != MapCreatorXml.BlankGroupHardcodedTile && !used.Contains(i)) return i;
        return -1;
    }

    private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);
    private static TerrainEditResult Fail(string m) => new(false, m, -1);
}

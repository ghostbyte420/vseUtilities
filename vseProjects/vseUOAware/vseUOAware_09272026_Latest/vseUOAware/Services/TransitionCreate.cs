using System.Globalization;
using System.Text;

namespace vseUOAware.Services;

internal sealed record CreateResult(bool Ok, string Report, string FilePath);

/// <summary>
/// Creates ONE new MapCreator transition rule (2-way or 3-way: any 1-9 distinct terrain IDs in the 3x3 key)
/// in a NEW output folder. Refuses a key that already exists anywhere (first duplicate wins in the compiler,
/// so a duplicate would be silently ignored). Never edits originals and never invents tile IDs.
/// </summary>
internal static class TransitionCreate
{
    public const string CustomSubPath = @"XMLFacetData\Custom\Transitions";

    public static CreateResult Create(string transitionsFolder, string terrainXmlPath, string outputFolder,
        string hashKey, string description, string mapTileIds, string? staticTileIds, string fileName,
        Func<int, string?>? commentFor)
    {
        if (!Directory.Exists(transitionsFolder)) return Fail("Transitions folder not found: " + transitionsFolder);
        if (!File.Exists(terrainXmlPath)) return Fail("Terrain.xml not found: " + terrainXmlPath);
        if (Directory.Exists(outputFolder) && Directory.EnumerateFileSystemEntries(outputFolder).Any())
            return Fail("Output folder is not empty (refusing to overwrite): " + outputFolder);

        string name = Path.GetFileName((fileName ?? "").Trim());
        if (name.Length == 0) return Fail("fileName is required (e.g. 'My Rule.xml').");
        if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) name += ".xml";
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return Fail("fileName has invalid characters.");

        var key = (hashKey ?? "").Trim().ToUpperInvariant();
        if (!MapCreatorXml.TryParseKey(key, out var ids)) return Fail("HashKey must be 18 hex characters (nine 2-hex terrain IDs).");
        if (string.IsNullOrWhiteSpace(description)) return Fail("description is required.");

        var maps = ParseList(mapTileIds, out var e1);
        if (e1 != null) return Fail(e1);
        var stats = ParseList(staticTileIds ?? "", out var e2);
        if (e2 != null) return Fail(e2);
        if (maps.Count == 0)
            return Fail("No MapTile IDs given. Without them MapCreator falls back to the Terrain.xml TileID; supply the land tile IDs you want (this tool never guesses).");

        var terrains = MapCreatorXml.LoadTerrains(terrainXmlPath);
        var tIds = terrains.Select(t => t.Id).ToHashSet();
        var entry = new TransEntry(name, description.Trim(), key, maps, stats, null);
        var errs = MapCreatorXml.Validate(entry, tIds);
        if (errs.Count > 0) return Fail(string.Join("\n", errs));

        var existing = MapCreatorXml.LoadTransitions(transitionsFolder).FirstOrDefault(
            t => string.Equals(t.HashKey, key, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return Fail($"Key {key} already exists in '{existing.File}' (first duplicate wins, a new copy would be ignored). Nothing written.");

        string path = Path.Combine(outputFolder, CustomSubPath, name);
        MapCreatorXml.WriteTransitionFile(path, new[] { entry }, commentFor);
        File.WriteAllText(Path.Combine(outputFolder, "INSTALL.md"), InstallText(name));

        int distinct = ids.Distinct().Count();
        string kind = distinct <= 2 ? "2-way" : distinct == 3 ? "3-way" : distinct + "-way";
        return new CreateResult(true,
            $"Created {kind} rule {key} with {maps.Count} map tile(s) and {stats.Count} static(s). Wrote {path} and INSTALL.md. Originals untouched.", path);

        static CreateResult Fail(string m) => new(false, m, "");
    }

    private static List<TileRef> ParseList(string text, out string? error)
    {
        error = null;
        var list = new List<TileRef>();
        foreach (var s in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int v = MapCreatorXml.ParseTileId(s);
            if (v < 0) { error = "bad tile ID '" + s + "'"; return list; }
            list.Add(new TileRef(v, 0));
        }
        return list;
    }

    private static string InstallText(string file) =>
        "# Install the new transition rule\n\n" +
        "1. Close MapCreator and Visual Studio.\n" +
        "2. Copy `XMLFacetData\\Custom\\Transitions\\" + file + "` from this output into your MapCreator source at\n" +
        "   `MapCreator.Engine\\Compiler\\XMLFacetData\\Custom\\Transitions\\` (keep the name; it is a NEW file).\n" +
        "3. No project edit needed: the csproj embeds everything under Compiler\\XMLFacetData\\**.\n" +
        "4. Rebuild MapCreator.Engine, run MapCreator, then re-run FindMissingTransitions to confirm.\n\n" +
        "Nothing in your original XMLFacetData was changed. Delete the copied file to undo.\n";
}

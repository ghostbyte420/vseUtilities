using System.Globalization;

namespace vseUOAware.Services;

internal sealed record CommonEditResult(bool Ok, string Report, string FilePath);

/// <summary>
/// Edits the "uniform" rules in Common.xml (HashKey = one terrain ID repeated 9 times, i.e. the plain fill tiles for a terrain).
/// Works on a COPY: writes XMLFacetData\Custom\Transitions\Common.xml (same relative path replaces the original at compile time)
/// into a new/empty folder plus INSTALL.md. Never edits the original. Never invents tile IDs.
/// </summary>
internal static class CommonEdit
{
    public const string CustomSubPath = @"XMLFacetData\Custom\Transitions";

    public static CommonEditResult Apply(string commonXmlPath, string terrainXmlPath, string outputFolder,
        int terrainId, string action, string mapTileIds, string? staticTileIds, string? description,
        Func<int, string?>? commentFor)
    {
        if (!File.Exists(commonXmlPath)) return Fail("Common.xml not found: " + commonXmlPath);
        if (!File.Exists(terrainXmlPath)) return Fail("Terrain.xml not found: " + terrainXmlPath);
        if (Directory.Exists(outputFolder) && Directory.EnumerateFileSystemEntries(outputFolder).Any())
            return Fail("Output folder is not empty (refusing to overwrite): " + outputFolder);
        if (terrainId < 0 || terrainId > 255) return Fail("terrainId must be 0-255.");

        string act = (action ?? "").Trim().ToLowerInvariant();
        if (act is not ("add" or "replace" or "remove")) return Fail("action must be add, replace or remove.");

        var terrains = MapCreatorXml.LoadTerrains(terrainXmlPath);
        var terrain = terrains.FirstOrDefault(t => t.Id == terrainId);
        if (terrain == null) return Fail($"Terrain ID {terrainId} (0x{terrainId:X2}) is not in Terrain.xml. Create it first with CreateTerrainType.");

        string key = MapCreatorXml.BuildKey(Enumerable.Repeat(terrainId, 9).ToArray());
        var entries = MapCreatorXml.LoadTransitionFile(commonXmlPath, Path.GetFileName(commonXmlPath));
        int idx = entries.FindIndex(e => string.Equals(e.HashKey, key, StringComparison.OrdinalIgnoreCase));

        if (act == "remove")
        {
            if (idx < 0) return Fail($"No uniform rule {key} in Common.xml; nothing to remove.");
            entries.RemoveAt(idx);
        }
        else
        {
            if (act == "add" && idx >= 0) return Fail($"Uniform rule {key} already exists; use action=replace.");
            if (act == "replace" && idx < 0) return Fail($"No uniform rule {key} to replace; use action=add.");
            var maps = ParseList(mapTileIds, out var e1);
            if (e1 != null) return Fail(e1);
            var stats = ParseList(staticTileIds ?? "", out var e2);
            if (e2 != null) return Fail(e2);
            if (maps.Count == 0) return Fail("No MapTile IDs given (this tool never guesses tile IDs).");
            string desc = string.IsNullOrWhiteSpace(description) ? "Common - " + terrain.Name : description.Trim();
            var entry = new TransEntry(Path.GetFileName(commonXmlPath), desc, key, maps, stats, null);
            var errs = MapCreatorXml.Validate(entry, terrains.Select(t => t.Id).ToHashSet());
            if (errs.Count > 0) return Fail(string.Join("\n", errs));
            if (idx >= 0) entries[idx] = entry; else entries.Add(entry);
        }

        string path = Path.Combine(outputFolder, CustomSubPath, "Common.xml");
        MapCreatorXml.WriteTransitionFile(path, entries, commentFor);
        File.WriteAllText(Path.Combine(outputFolder, "INSTALL.md"),
            "# Install the edited Common.xml\n\n" +
            "1. Close MapCreator and Visual Studio.\n" +
            "2. Copy `XMLFacetData\\Custom\\Transitions\\Common.xml` into `MapCreator.Engine\\Compiler\\XMLFacetData\\Custom\\Transitions\\`.\n" +
            "   It has the same relative name as the original, so it replaces the original at compile time.\n" +
            "3. Rebuild MapCreator.Engine and run MapCreator.\n\n" +
            "The original Common.xml was not changed. Delete the copied file to undo.\n");
        return new CommonEditResult(true,
            string.Create(CultureInfo.InvariantCulture, $"{act} done for terrain {terrainId} '{terrain.Name}' (key {key}). Common.xml now has {entries.Count} rules. Wrote {path} and INSTALL.md. Original untouched."), path);

        static CommonEditResult Fail(string m) => new(false, m, "");
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
}

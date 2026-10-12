using System.Globalization;
using System.Text;

namespace vseUOAware.Services;

internal sealed record RepairResult(bool Ok, string Report, int FilesWritten, int Dropped, int Trimmed, int Filled);

/// <summary>
/// Conservative repair of MapCreator transition XML. Only fixes what can be proven broken
/// (malformed keys, later duplicate keys the compiler already ignores, out-of-range tile refs).
/// Missing keys are filled ONLY from tile IDs the caller supplies. Never edits originals.
/// </summary>
internal static class TransitionRepair
{
    public const string CustomSubPath = @"XMLFacetData\Custom\Transitions";
    public const string FillsFileName = "RepairFills.xml";

    /// <summary>Fill line format: HashKey|Description|mapTileIds(comma)|staticIds(comma, optional)</summary>
    public static bool TryParseFill(string line, ISet<int> terrainIds, out TransEntry? entry, out string error)
    {
        entry = null;
        error = "";
        var p = line.Split('|', StringSplitOptions.TrimEntries);
        if (p.Length < 3) { error = "needs HashKey|Description|mapTileIds[|staticIds]"; return false; }
        var key = p[0].ToUpperInvariant();
        if (!MapCreatorXml.TryParseKey(key, out var ids)) { error = "HashKey must be 18 hex characters"; return false; }
        foreach (var id in ids.Distinct().Where(i => !terrainIds.Contains(i)))
        { error = "terrain ID " + id.ToString(CultureInfo.InvariantCulture) + " is not in Terrain.xml"; return false; }
        var maps = ParseIds(p[2], out var e1);
        if (e1 != null) { error = e1; return false; }
        string? e2 = null;
        var stats = p.Length > 3 ? ParseIds(p[3], out e2) : new List<TileRef>();
        if (e2 != null) { error = e2; return false; }
        var tmp = new TransEntry("", p[1], key, maps, stats, null);
        var errs = MapCreatorXml.Validate(tmp, terrainIds);
        if (errs.Count > 0) { error = errs[0]; return false; }
        entry = tmp;
        return true;
    }

    private static List<TileRef> ParseIds(string text, out string? error)
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

    public static RepairResult Run(string transitionsFolder, string terrainXmlPath, string outputFolder,
        IEnumerable<string> fillLines, Func<int, string?>? commentFor)
    {
        var sb = new StringBuilder();
        if (!Directory.Exists(transitionsFolder)) return Fail("Transitions folder not found: " + transitionsFolder);
        if (!File.Exists(terrainXmlPath)) return Fail("Terrain.xml not found: " + terrainXmlPath);
        if (Directory.Exists(outputFolder) && Directory.EnumerateFileSystemEntries(outputFolder).Any())
            return Fail("Output folder is not empty (refusing to overwrite): " + outputFolder);

        var terrains = MapCreatorXml.LoadTerrains(terrainXmlPath);
        var tIds = terrains.Select(t => t.Id).ToHashSet();
        var entries = MapCreatorXml.LoadTransitions(transitionsFolder);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<TransEntry>();
        var changedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int dropped = 0, trimmed = 0, unknownTerrain = 0;
        foreach (var e in entries)
        {
            if (!MapCreatorXml.TryParseKey(e.HashKey, out _))
            {
                dropped++; changedFiles.Add(e.File);
                sb.AppendLine(CultureInfo.InvariantCulture, $"DROPPED malformed key '{e.HashKey}' in {e.File}");
                continue;
            }
            if (!seen.Add(e.HashKey))
            {
                dropped++; changedFiles.Add(e.File);
                sb.AppendLine(CultureInfo.InvariantCulture, $"DROPPED duplicate key {e.HashKey} in {e.File} (compiler already ignores it)");
                continue;
            }
            var maps = e.MapTiles.Where(m => m.TileId >= 0 && m.TileId <= MapCreatorXml.MaxLandTileId).ToList();
            var stats = e.Statics.Where(s => s.TileId >= 0 && s.TileId <= short.MaxValue).ToList();
            int removed = (e.MapTiles.Count - maps.Count) + (e.Statics.Count - stats.Count);
            var cur = e;
            if (removed > 0)
            {
                trimmed += removed; changedFiles.Add(e.File);
                sb.AppendLine(CultureInfo.InvariantCulture, $"TRIMMED {removed} out-of-range tile ref(s) from {e.HashKey} in {e.File}");
                cur = e with { MapTiles = maps, Statics = stats };
            }
            if (MapCreatorXml.Validate(cur, tIds).Any(x => x.Contains("not in Terrain.xml", StringComparison.Ordinal)))
            {
                unknownTerrain++;
                sb.AppendLine(CultureInfo.InvariantCulture, $"REPORT-ONLY unknown terrain ID in key {e.HashKey} in {e.File} (kept; add the terrain to Terrain.xml or remove the rule yourself)");
            }
            kept.Add(cur);
        }

        int written = 0;
        var outDir = Path.Combine(outputFolder, CustomSubPath);
        foreach (var g in kept.GroupBy(k => k.File, StringComparer.OrdinalIgnoreCase).Where(g => changedFiles.Contains(g.Key)))
        {
            MapCreatorXml.WriteTransitionFile(Path.Combine(outDir, g.Key), g, commentFor);
            written++;
        }
        foreach (var f in changedFiles.Where(f => !kept.Any(k => string.Equals(k.File, f, StringComparison.OrdinalIgnoreCase))))
        {
            MapCreatorXml.WriteTransitionFile(Path.Combine(outDir, f), Array.Empty<TransEntry>(), commentFor);
            written++;
        }

        var fills = new List<TransEntry>();
        foreach (var line in fillLines.Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            if (!TryParseFill(line, tIds, out var fe, out var err)) { sb.AppendLine("FILL REJECTED '" + line + "': " + err); continue; }
            if (!seen.Add(fe!.HashKey)) { sb.AppendLine("FILL SKIPPED " + fe.HashKey + ": key already exists"); continue; }
            fills.Add(fe with { File = FillsFileName });
        }
        if (fills.Count > 0)
        {
            MapCreatorXml.WriteTransitionFile(Path.Combine(outDir, FillsFileName), fills, commentFor);
            written++;
        }

        var head = new StringBuilder();
        head.AppendLine(CultureInfo.InvariantCulture, $"Scanned {entries.Count} transition entries. Dropped {dropped}, trimmed {trimmed}, filled {fills.Count}, unknown-terrain reported {unknownTerrain}. Files written {written}.");
        head.AppendLine("Original files were not modified.");
        string report = head.ToString() + sb;
        Directory.CreateDirectory(outputFolder);
        File.WriteAllText(Path.Combine(outputFolder, "REPORT.md"), "# Repair report\n\n" + report);
        File.WriteAllText(Path.Combine(outputFolder, "INSTALL.md"), InstallText(written));
        return new RepairResult(true, report, written, dropped, trimmed, fills.Count);

        static RepairResult Fail(string m) => new(false, m, 0, 0, 0, 0);
    }

    private static string InstallText(int files) =>
        "# Install the repaired transition XML\n\n" +
        "Files written: " + files.ToString(CultureInfo.InvariantCulture) + "\n\n" +
        "1. Close MapCreator and Visual Studio.\n" +
        "2. Copy the folder `XMLFacetData\\Custom\\Transitions` from this output into your MapCreator source at\n" +
        "   `MapCreator.Engine\\Compiler\\XMLFacetData\\Custom\\Transitions` (merge; keep the sub-folder structure).\n" +
        "   Files with the same relative path as an original REPLACE that original at compile time.\n" +
        "3. No project edit needed: MapCreator.Engine.csproj already embeds everything under Compiler\\XMLFacetData\\**, including Custom.\n" +
        "4. Rebuild MapCreator.Engine, then run MapCreator.\n" +
        "5. Re-run FindMissingTransitions on your Terrain.bmp to confirm.\n\n" +
        "Nothing in your original XMLFacetData was changed. Delete the copied files to undo.\n" +
        "See REPORT.md for exactly what was changed.\n";
}

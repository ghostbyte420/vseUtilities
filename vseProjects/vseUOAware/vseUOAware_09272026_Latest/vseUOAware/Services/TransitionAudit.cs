using System.Globalization;
using System.Text;

namespace vseUOAware.Services;

internal sealed record AuditResult(bool Ok, string Report, int Files, int Delete, int Redo, int Keep);

/// <summary>
/// Read-only audit of a user's MapCreator transition XML against their Terrain.xml.
/// Classifies every file: KEEP, REDO (some rules use terrain IDs missing from Terrain.xml or have no map tiles),
/// DELETE-CANDIDATE (empty, or every rule is broken). Also lists duplicate keys the compiler would ignore.
/// Writes AUDIT.md only into a new/empty output folder. Never edits any XML.
/// </summary>
internal static class TransitionAudit
{
    public static AuditResult Run(string transitionsFolder, string terrainXmlPath, string outputFolder)
    {
        if (!Directory.Exists(transitionsFolder)) return Fail("Transitions folder not found: " + transitionsFolder);
        if (!File.Exists(terrainXmlPath)) return Fail("Terrain.xml not found: " + terrainXmlPath);
        if (Directory.Exists(outputFolder) && Directory.EnumerateFileSystemEntries(outputFolder).Any())
            return Fail("Output folder is not empty (refusing to overwrite): " + outputFolder);

        var terrains = MapCreatorXml.LoadTerrains(terrainXmlPath);
        var tIds = terrains.Select(t => t.Id).ToHashSet();
        var names = terrains.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First().Name);

        var files = Directory.EnumerateFiles(transitionsFolder, "*.xml", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        var perFile = new List<(string Label, int Total, int Broken, int NoTiles, SortedSet<int> Missing)>();
        var all = new List<TransEntry>();
        var missingUse = new Dictionary<int, int>();
        foreach (var f in files)
        {
            string label = Path.GetRelativePath(transitionsFolder, f);
            var entries = MapCreatorXml.LoadTransitionFile(f, label);
            all.AddRange(entries);
            int broken = 0, noTiles = 0;
            var miss = new SortedSet<int>();
            foreach (var e in entries)
            {
                bool bad = false;
                if (MapCreatorXml.TryParseKey(e.HashKey, out var ids))
                {
                    foreach (var id in ids.Distinct().Where(i => !tIds.Contains(i)))
                    {
                        miss.Add(id);
                        missingUse[id] = missingUse.GetValueOrDefault(id) + 1;
                        bad = true;
                    }
                }
                else bad = true;
                if (e.MapTiles.Count == 0) noTiles++;
                if (bad) broken++;
            }
            perFile.Add((label, entries.Count, broken, noTiles, miss));
        }

        var dups = MapCreatorXml.FindDuplicates(all);
        var deleteList = perFile.Where(p => p.Total == 0 || p.Broken == p.Total).ToList();
        var redoList = perFile.Where(p => p.Total > 0 && p.Broken > 0 && p.Broken < p.Total).ToList();
        var noTileList = perFile.Where(p => p.NoTiles > 0 && p.Broken == 0).ToList();
        int keep = perFile.Count - deleteList.Count - redoList.Count;

        var sb = new StringBuilder();
        sb.AppendLine("# Transition XML audit");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Files: {perFile.Count}. Rules: {all.Count}. Terrains in Terrain.xml: {terrains.Count}.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"KEEP {keep}, REDO {redoList.Count}, DELETE-CANDIDATE {deleteList.Count}, duplicate keys {dups.Count}, files with rules lacking MapTiles {noTileList.Count}.");
        sb.AppendLine("Originals were not modified. Nothing here is deleted for you.");
        sb.AppendLine();
        sb.AppendLine("## Terrain IDs used in HashKeys but missing from Terrain.xml");
        sb.AppendLine("HashKey values are Terrain.xml group IDs (hex pairs), not land or static tile IDs.");
        if (missingUse.Count == 0) sb.AppendLine("None.");
        foreach (var kv in missingUse.OrderBy(k => k.Key))
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {kv.Key:X2} (decimal {kv.Key}): used in {kv.Value} rule(s)");
        sb.AppendLine();
        sb.AppendLine("## DELETE-CANDIDATE (empty, or every rule is broken)");
        foreach (var p in deleteList) sb.AppendLine(Line(p, names));
        if (deleteList.Count == 0) sb.AppendLine("None.");
        sb.AppendLine();
        sb.AppendLine("## REDO (some rules broken)");
        foreach (var p in redoList) sb.AppendLine(Line(p, names));
        if (redoList.Count == 0) sb.AppendLine("None.");
        sb.AppendLine();
        sb.AppendLine("## Rules with no MapTiles (compiler falls back to Terrain.xml TileID)");
        foreach (var p in noTileList) sb.AppendLine(CultureInfo.InvariantCulture, $"- {p.Label}: {p.NoTiles} rule(s)");
        if (noTileList.Count == 0) sb.AppendLine("None.");
        sb.AppendLine();
        sb.AppendLine("## Duplicate keys (first wins, the later copy is ignored)");
        foreach (var d in dups.Take(500)) sb.AppendLine(CultureInfo.InvariantCulture, $"- {d.HashKey}: kept in {d.WinnerFile}, ignored in {d.LoserFile}");
        if (dups.Count == 0) sb.AppendLine("None.");
        else if (dups.Count > 500) sb.AppendLine(CultureInfo.InvariantCulture, $"(+{dups.Count - 500} more)");

        Directory.CreateDirectory(outputFolder);
        string report = sb.ToString();
        File.WriteAllText(Path.Combine(outputFolder, "AUDIT.md"), report);
        string head = $"Files {perFile.Count}: KEEP {keep}, REDO {redoList.Count}, DELETE-CANDIDATE {deleteList.Count}; duplicate keys {dups.Count}; missing terrain IDs {missingUse.Count}. Wrote {Path.Combine(outputFolder, "AUDIT.md")}. Originals untouched.";
        return new AuditResult(true, head, perFile.Count, deleteList.Count, redoList.Count, keep);

        static AuditResult Fail(string m) => new(false, m, 0, 0, 0, 0);
    }

    private static string Line((string Label, int Total, int Broken, int NoTiles, SortedSet<int> Missing) p, Dictionary<int, string> names)
    {
        string miss = p.Missing.Count == 0 ? "" : "; missing IDs " + string.Join(", ", p.Missing.Select(i => i.ToString("X2", CultureInfo.InvariantCulture)));
        return string.Create(CultureInfo.InvariantCulture, $"- {p.Label}: {p.Broken}/{p.Total} broken{miss}");
    }
}

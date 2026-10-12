using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace vseUOAware.Services;

internal sealed record TerrainDef(string Name, int Id, int TileId, int R, int G, int B, int Base, bool Random);

internal sealed record TileRef(int TileId, int AltIdMod);

internal sealed record TransEntry(string File, string Description, string HashKey,
    List<TileRef> MapTiles, List<TileRef> Statics, string? RandomFile);

internal sealed record DuplicateKey(string HashKey, string WinnerFile, string LoserFile);

/// <summary>Shared reader/validator/writer for MapCreator Terrain.xml and Transitions XML, following MapCreator.Engine rules.</summary>
internal static class MapCreatorXml
{
    public const int BlankGroupHardcodedTile = 254;
    public const int MaxLandTileId = 0x3FFF;

    public static List<TerrainDef> LoadTerrains(string terrainXmlPath)
    {
        var doc = XDocument.Load(terrainXmlPath);
        var list = new List<TerrainDef>();
        foreach (var e in doc.Descendants("Terrain"))
        {
            list.Add(new TerrainDef(
                (string?)e.Attribute("Name") ?? "",
                ParseInt(e, "ID"), ParseInt(e, "TileID"),
                ParseInt(e, "R"), ParseInt(e, "G"), ParseInt(e, "B"),
                ParseInt(e, "Base"),
                string.Equals((string?)e.Attribute("Random"), "true", StringComparison.OrdinalIgnoreCase)));
        }
        return list;
    }

    /// <summary>Loads every *.xml under a folder, ordered by relative path (ordinal ignore case) like FacetData.EnumerateXml.</summary>
    public static List<TransEntry> LoadTransitions(string folder)
    {
        var files = Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories)
            .Select(f => (Full: f, Rel: Path.GetRelativePath(folder, f)))
            .OrderBy(t => t.Rel, StringComparer.OrdinalIgnoreCase);
        var all = new List<TransEntry>();
        foreach (var (full, rel) in files)
            all.AddRange(LoadTransitionFile(full, rel));
        return all;
    }

    public static List<TransEntry> LoadTransitionFile(string path, string? label = null)
    {
        var doc = XDocument.Load(path);
        var list = new List<TransEntry>();
        foreach (var t in doc.Descendants("TransInfo"))
        {
            list.Add(new TransEntry(label ?? path,
                (string?)t.Attribute("Description") ?? "",
                ((string?)t.Attribute("HashKey") ?? "").ToUpperInvariant(),
                ReadTiles(t.Element("MapTiles"), "MapTile"),
                ReadTiles(t.Element("StaticTiles"), "StaticTile"),
                (string?)t.Attribute("File")));
        }
        return list;
    }

    /// <summary>Compiler keeps the first key it loads; later duplicates are silently skipped.</summary>
    public static List<DuplicateKey> FindDuplicates(IEnumerable<TransEntry> orderedEntries)
    {
        var first = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dups = new List<DuplicateKey>();
        foreach (var e in orderedEntries)
        {
            if (first.TryGetValue(e.HashKey, out var winner)) dups.Add(new DuplicateKey(e.HashKey, winner, e.File));
            else first[e.HashKey] = e.File;
        }
        return dups;
    }

    public static bool TryParseKey(string key, out int[] ids)
    {
        ids = new int[9];
        if (key.Length != 18) return false;
        for (int i = 0; i < 9; i++)
            if (!int.TryParse(key.AsSpan(i * 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ids[i])) return false;
        return true;
    }

    public static string BuildKey(IReadOnlyList<int> ids) =>
        string.Concat(ids.Select(i => i.ToString("X2", CultureInfo.InvariantCulture)));

    public static int DistinctTerrainCount(string key) =>
        TryParseKey(key, out var ids) ? ids.Distinct().Count() : 0;

    /// <summary>Returns problems that would break or silently weaken a compile. Empty = OK.</summary>
    public static List<string> Validate(TransEntry e, ISet<int> terrainIds)
    {
        var errors = new List<string>();
        if (!TryParseKey(e.HashKey, out var ids))
            errors.Add($"HashKey '{e.HashKey}' must be 18 hex characters (nine 2-hex terrain IDs).");
        else
            foreach (var id in ids.Distinct().Where(i => !terrainIds.Contains(i)))
                errors.Add($"HashKey '{e.HashKey}' uses terrain ID {id} (0x{id:X2}) not in Terrain.xml.");
        foreach (var m in e.MapTiles)
            if (m.TileId < 0 || m.TileId > MaxLandTileId)
                errors.Add($"HashKey '{e.HashKey}': MapTile {m.TileId} outside land range 0-{MaxLandTileId}.");
        foreach (var s in e.Statics)
            if (s.TileId < 0 || s.TileId > short.MaxValue)
                errors.Add($"HashKey '{e.HashKey}': StaticTile {s.TileId} outside 0-{short.MaxValue}.");
        return errors;
    }

    public static List<string> ValidateTerrains(IReadOnlyList<TerrainDef> terrains)
    {
        var errors = new List<string>();
        foreach (var g in terrains.GroupBy(t => t.Id).Where(g => g.Count() > 1))
            errors.Add($"Terrain ID {g.Key} used more than once: {string.Join(", ", g.Select(t => t.Name))}.");
        foreach (var g in terrains.GroupBy(t => (t.R, t.G, t.B)).Where(g => g.Count() > 1))
            errors.Add($"Colour {g.Key.R},{g.Key.G},{g.Key.B} shared by: {string.Join(", ", g.Select(t => t.Name))}.");
        foreach (var t in terrains)
        {
            if (t.Id < 0 || t.Id > 255) errors.Add($"Terrain '{t.Name}' ID {t.Id} outside 0-255.");
            if (t.Id == BlankGroupHardcodedTile) errors.Add($"Terrain '{t.Name}' uses ID 254, which the compiler hardcodes to tile 1078.");
            if (t.R > 255 || t.G > 255 || t.B > 255 || t.Base > 255) errors.Add($"Terrain '{t.Name}' has a byte value above 255.");
        }
        return errors;
    }

    /// <summary>Writes a transition file in the compiler's format. commentFor(landTileId) may return e.g. "texture 1263 / name".</summary>
    public static void WriteTransitionFile(string path, IEnumerable<TransEntry> entries, Func<int, string?>? commentFor = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", Encoding = new UTF8Encoding(false) };
        using var w = XmlWriter.Create(path, settings);
        w.WriteStartDocument();
        w.WriteStartElement("Trans");
        foreach (var e in entries)
        {
            w.WriteStartElement("TransInfo");
            w.WriteAttributeString("Description", e.Description);
            w.WriteAttributeString("HashKey", e.HashKey);
            if (!string.IsNullOrEmpty(e.RandomFile)) w.WriteAttributeString("File", e.RandomFile);
            w.WriteStartElement("MapTiles");
            foreach (var m in e.MapTiles)
            {
                var c = commentFor?.Invoke(m.TileId);
                if (!string.IsNullOrEmpty(c)) w.WriteComment(" " + c.Replace("--", "- -") + " ");
                w.WriteStartElement("MapTile");
                w.WriteAttributeString("TileID", m.TileId.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("AltIDMod", m.AltIdMod.ToString(CultureInfo.InvariantCulture));
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteStartElement("StaticTiles");
            foreach (var s in e.Statics)
            {
                w.WriteStartElement("StaticTile");
                w.WriteAttributeString("TileID", s.TileId.ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("AltIDMod", s.AltIdMod.ToString(CultureInfo.InvariantCulture));
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndElement();
        }
        w.WriteEndElement();
        w.WriteEndDocument();
    }

    private static List<TileRef> ReadTiles(XElement? parent, string name)
    {
        var list = new List<TileRef>();
        if (parent == null) return list;
        foreach (var t in parent.Elements(name))
            list.Add(new TileRef(ParseTileId((string?)t.Attribute("TileID")), ParseInt(t, "AltIDMod")));
        return list;
    }

    /// <summary>Compiler tries decimal first, then hex (as "&amp;H" + text).</summary>
    public static int ParseTileId(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return -1;
        if (short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)) return d;
        var h = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
        return int.TryParse(h, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var x) ? x : -1;
    }

    private static int ParseInt(XElement e, string attr) =>
        int.TryParse((string?)e.Attribute(attr), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}

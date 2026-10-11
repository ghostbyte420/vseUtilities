using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Read-only water diagnostics. "Water" is decided from tiledata's Wet flag on LAND tiles,
/// not from hard-coded IDs, so it works for custom maps. Never writes to map/patch files.
/// </summary>
internal class WaterTools
{
    private const int MaxArea = 512 * 512;

    [McpServerTool]
    [Description("Finds water (tiledata Wet land tiles = deep water, plus any land tile under a Wet static = shallow bed) that touch non-water land in a map region. Lists tile ID, world X/Y, Z and touching shore tile IDs, and summarises coast heights (shore Z minus water Z) because coast heights vary by design. Only fault flagged: a water surface outside -120..120. Read-only; optional CSV to a separate output folder.")]
    public string FindDeepWaterNearShore(
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles (max area 512x512).")] int width,
        [Description("Region height in tiles (max area 512x512).")] int height,
        [Description("Facet (map) number: 0-5 are the defaults, any higher number is a custom map (Map6, Map32, Map10000...). If given, map/statics/index paths are filled in from the saved UO folder.")] int? facet = null,
        [Description("Full path to mapN.mul (e.g. map4.mul) or mapNLegacyMUL.uop. Not needed when facet is given.")] string? mapFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Full path to tiledata.mul. Not needed when the saved UO folder has one.")] string? tileDataFilePath = null,
        [Description("Facet width in 8x8 blocks. Not needed for default facets 0-5. For custom maps give it and the height is worked out from the file.")] int facetWidthBlocks = 0,
        [Description("Only list shore tiles whose land is at least this many z above the water surface (0 = list all). Coast heights vary by design, so this is a filter, not a fault.")] int stepThreshold = 0,
        [Description("Max shore tiles to list (default 60).")] int maxResults = 60,
        [Description("Optional folder to write a CSV of every hit into (must not be the map folder).")] string? outputFolder = null)
    {
        if (facet.HasValue)
        {
            if (!FacetResolver.TryResolve(facet.Value, null, facetWidthBlocks > 0 ? facetWidthBlocks : null, out var fp, out var fe))
            {
                return fe;
            }

            mapFilePath = fp.MapFile;
            staticsMulFilePath = fp.StaticsFile;
            staIdxMulFilePath = fp.StaIdxFile;
            facetWidthBlocks = fp.WidthBlocks;
        }

        if (string.IsNullOrEmpty(tileDataFilePath))
        {
            var uoDir = DirectoryService.LoadSavedDirectory();
            if (!string.IsNullOrWhiteSpace(uoDir))
            {
                tileDataFilePath = Path.Combine(uoDir, "tiledata.mul");
            }
        }

        if (string.IsNullOrEmpty(mapFilePath) || string.IsNullOrEmpty(staticsMulFilePath) || string.IsNullOrEmpty(staIdxMulFilePath) || string.IsNullOrEmpty(tileDataFilePath))
        {
            return "Give a facet number, or the map, statics, staidx and tiledata file paths.";
        }

        if (facetWidthBlocks <= 0)
        {
            return "facetWidthBlocks is required when no facet number is given.";
        }

        if (!File.Exists(mapFilePath) || !File.Exists(tileDataFilePath))
        {
            return "Map file or tiledata.mul not found.";
        }

        if (width < 1 || height < 1 || (long)width * height > MaxArea)
        {
            return $"Region too large or empty: max area is {MaxArea} tiles.";
        }

        int rx = Math.Max(0, x - 1), ry = Math.Max(0, y - 1);
        int rw = width + 2, rh = height + 2;
        var cells = MapCoordinateReader.GetRegion(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, rx, ry, rw, rh, includeStatics: true);

        var flagCache = new Dictionary<int, (bool Wet, bool Impassable, int Texture, string Name)>();
        (bool Wet, bool Impassable, int Texture, string Name) Info(int id)
        {
            if (!flagCache.TryGetValue(id, out var v))
            {
                var d = TileDataReader.GetLandDetail(tileDataFilePath, id & 0x3FFF);
                v = (d.Flags.HasFlag(TileFlag.Wet), d.Flags.HasFlag(TileFlag.Impassable), d.TextureId, d.Name);
                flagCache[id] = v;
            }

            return v;
        }

        MapCoordinateInfo At(int wx, int wy) => cells[(wy - ry) * rw + (wx - rx)];

        var staticWetCache = new Dictionary<int, bool>();
        bool StaticIsWet(int id)
        {
            if (!staticWetCache.TryGetValue(id, out var w))
            {
                w = TileDataReader.GetItemDetail(tileDataFilePath, 0x4000 + id).Flags.HasFlag(TileFlag.Wet);
                staticWetCache[id] = w;
            }

            return w;
        }

        bool IsShallowBed(MapCoordinateInfo c) => !Info(c.LandTileId).Wet && c.Statics.Any(s => StaticIsWet(s.TileId));
        bool IsWater(MapCoordinateInfo c) => Info(c.LandTileId).Wet || IsShallowBed(c);
        var shallowBedCounts = new SortedDictionary<int, int>();
        int WaterZ(MapCoordinateInfo c) => IsShallowBed(c) ? c.Statics.Where(s => StaticIsWet(s.TileId)).Max(s => s.Z) : c.LandZ;
        int Bucket(int d) => d < 0 ? 0 : d == 0 ? 1 : d < 5 ? 2 : d == 5 ? 3 : d < 10 ? 4 : d < 25 ? 5 : 6;
        string[] bucketNames = ["below water", "level (0)", "1-4 above", "5 above", "6-9 above", "10-24 above", "25+ above (cliff)"];
        var heightBuckets = new int[7];

        var hits = new List<string>();
        var csv = new StringBuilder("x,y,waterTileId,waterZ,shoreTileIds,problem\n");
        var waterIdCounts = new SortedDictionary<int, int>();
        var shoreIdCounts = new SortedDictionary<int, int>();
        int waterTotal = 0, shoreHits = 0, problems = 0;

        for (int wy = y; wy < y + height; wy++)
        {
            for (int wx = x; wx < x + width; wx++)
            {
                if (wx < rx || wy < ry || wx - rx >= rw || wy - ry >= rh)
                {
                    continue;
                }

                var c = At(wx, wy);
                if (!IsWater(c))
                {
                    continue;
                }

                waterTotal++;
                if (IsShallowBed(c))
                {
                    shallowBedCounts[c.LandTileId] = shallowBedCounts.GetValueOrDefault(c.LandTileId) + 1;
                }
                else
                {
                    waterIdCounts[c.LandTileId] = waterIdCounts.GetValueOrDefault(c.LandTileId) + 1;
                }

                var shore = new SortedSet<int>();
                int maxStep = int.MinValue, waterZ = WaterZ(c);
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || wx + dx < rx || wy + dy < ry || wx + dx - rx >= rw || wy + dy - ry >= rh)
                        {
                            continue;
                        }

                        var n = At(wx + dx, wy + dy);
                        if (!IsWater(n))
                        {
                            shore.Add(n.LandTileId);
                            maxStep = Math.Max(maxStep, n.LandZ - waterZ);
                        }
                    }
                }

                if (shore.Count == 0)
                {
                    continue;
                }

                shoreHits++;
                foreach (int s in shore)
                {
                    shoreIdCounts[s] = shoreIdCounts.GetValueOrDefault(s) + 1;
                }

                string problem = Math.Abs(waterZ) > 120 ? $"water surface Z {waterZ} is outside -120..120 (hard limit 127)" : string.Empty;
                heightBuckets[Bucket(maxStep)]++;
                if (problem.Length > 0)
                {
                    problems++;
                }

                string shoreText = string.Join("/", shore);
                csv.AppendLine($"{wx},{wy},{c.LandTileId},{c.LandZ},{shoreText},{problem}");
                if (hits.Count < maxResults && (stepThreshold <= 0 || maxStep >= stepThreshold))
                {
                    hits.Add($"  ({wx},{wy}) water {c.LandTileId} z={c.LandZ} touches shore tiles [{shoreText}]{(problem.Length > 0 ? "  <-- " + problem : string.Empty)}");
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Region ({x},{y}) {width}x{height}: {waterTotal} water tiles (deep land + shallow beds), {shoreHits} touch non-water land, {problems} with a water surface outside -120..120.");
        sb.AppendLine("Coast height (shore Z minus water Z) per shore-touching water tile: " + string.Join(", ", bucketNames.Select((nm, k) => $"{nm} x{heightBuckets[k]}")));
        sb.AppendLine("Deep water land tile IDs (Wet flag, no static): " + string.Join(", ", waterIdCounts.Select(k => $"{k.Key}x{k.Value}")));
        sb.AppendLine("Shallow-water beds (land tile under a water static): " + string.Join(", ", shallowBedCounts.Select(k => $"{k.Key}x{k.Value}")));
        sb.AppendLine("Shore tile IDs touched: " + string.Join(", ", shoreIdCounts.Select(k => $"{k.Key} '{Info(k.Key).Name}' x{k.Value}")));
        foreach (var h in hits)
        {
            sb.AppendLine(h);
        }

        if (shoreHits > hits.Count)
        {
            sb.AppendLine($"  ... {shoreHits - hits.Count} more not shown.");
        }

        if (!string.IsNullOrWhiteSpace(outputFolder))
        {
            string mapDir = Path.GetDirectoryName(Path.GetFullPath(mapFilePath))!;
            string outDir = Path.GetFullPath(outputFolder);
            if (string.Equals(outDir.TrimEnd('\\'), mapDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("CSV NOT written: output folder must differ from the map folder.");
            }
            else
            {
                Directory.CreateDirectory(outDir);
                string file = Path.Combine(outDir, $"waterShore_{x}_{y}_{width}x{height}.csv");
                File.WriteAllText(file, csv.ToString());
                sb.AppendLine("CSV written: " + file);
            }
        }

        return sb.ToString();
    }
}

using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Map-editor style tools: isometric region rendering from classic MUL/IDX art
/// and land-corner/transition diagnostics.
/// </summary>
internal class MapEditorTools
{
    private const int MaxRegion = 96;

    [McpServerTool]
    [Description("Renders an isometric PNG of a map region (land + statics) using classic artidx.mul/art.mul. Land is placed by Z (4px per Z), statics are anchored to their tile and sorted by (x+y), z, land-first. Hues are not applied. Max 96x96 tiles.")]
    public string RenderMapRegion(
        [Description("Full path to mapN.mul or mapNLegacyMUL.uop.")] string mapFilePath,
        [Description("Full path to staticsN.mul.")] string staticsMulFilePath,
        [Description("Full path to staidxN.mul.")] string staIdxMulFilePath,
        [Description("Full path to artidx.mul.")] string artIdxFilePath,
        [Description("Full path to art.mul.")] string artMulFilePath,
        [Description("Facet width in 8x8 blocks (e.g. 896 for Felucca/Trammel).")] int facetWidthBlocks,
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles (max 96).")] int width,
        [Description("Region height in tiles (max 96).")] int height,
        [Description("Output PNG path or bare filename.")] string? outputPngPath = null)
    {
        foreach (var f in new[] { mapFilePath, staticsMulFilePath, staIdxMulFilePath, artIdxFilePath, artMulFilePath })
            if (!File.Exists(f)) return $"File not found: {f}";
        if (width < 1 || height < 1 || width > MaxRegion || height > MaxRegion)
            return $"Width/height must be 1-{MaxRegion}.";

        try
        {
            var region = MapCoordinateReader.GetRegion(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, width, height);
            var cache = new Dictionary<int, ArtImage?>();
            ArtImage? Art(int id)
            {
                if (!cache.TryGetValue(id, out var img))
                    cache[id] = img = ArtReader.GetArtTileMul(artIdxFilePath, artMulFilePath, id);
                return img;
            }

            // (sortX+Y, z, layer, image, left, top)
            var draws = new List<(int Depth, int Z, int Layer, ArtImage Img, int Left, int Top)>();
            foreach (var c in region)
            {
                int rx = c.X - x, ry = c.Y - y;
                int sx = (rx - ry) * 22;
                int sy = (rx + ry) * 22;

                var land = Art(c.LandTileId);
                if (land is not null)
                    draws.Add((rx + ry, c.LandZ, 0, land, sx, sy - c.LandZ * 4));

                int order = 1;
                foreach (var s in c.Statics)
                {
                    var img = Art(0x4000 + s.TileId);
                    if (img is null) continue;
                    draws.Add((rx + ry, s.Z, order++, img, sx + 22 - img.Width / 2, sy + 22 - img.Height - s.Z * 4));
                }
            }
            if (draws.Count == 0) return "Nothing to draw in that region.";

            int minX = draws.Min(d => d.Left), minY = draws.Min(d => d.Top);
            int maxX = draws.Max(d => d.Left + d.Img.Width), maxY = draws.Max(d => d.Top + d.Img.Height);
            int w = maxX - minX, h = maxY - minY;
            var canvas = new byte[w * h * 4];

            foreach (var d in draws.OrderBy(d => d.Depth).ThenBy(d => d.Z).ThenBy(d => d.Layer))
            {
                for (int py = 0; py < d.Img.Height; py++)
                {
                    for (int px = 0; px < d.Img.Width; px++)
                    {
                        int si = (py * d.Img.Width + px) * 4;
                        if (d.Img.Rgba[si + 3] == 0) continue;
                        int di = ((d.Top - minY + py) * w + (d.Left - minX + px)) * 4;
                        Buffer.BlockCopy(d.Img.Rgba, si, canvas, di, 4);
                    }
                }
            }

            string path = ExportPathResolver.Resolve(outputPngPath, $"region_{x}_{y}_{width}x{height}.png", "Map");
            PngImageWriter.SaveRgbaAsPng(canvas, w, h, path);
            return $"Rendered region ({x},{y}) {width}x{height} -> {w}x{h}px, {draws.Count} sprites, to: {path}";
        }
        catch (Exception ex)
        {
            return $"Failed to render region: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Finds land tiles in a region that will render wrongly because of height: a SLOPED tile (corner Zs differ) whose tiledata TextureId is 0 can only be drawn as a flat art diamond, leaving gaps/overlaps (broken transition triangles). Also reports steep steps (>= cliffThreshold). Requires tiledata.mul.")]
    public string FindHeightProblems(
        [Description("Full path to mapN.mul or mapNLegacyMUL.uop.")] string mapFilePath,
        [Description("Full path to staticsN.mul.")] string staticsMulFilePath,
        [Description("Full path to staidxN.mul.")] string staIdxMulFilePath,
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks,
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles (max 96).")] int width,
        [Description("Region height in tiles (max 96).")] int height,
        [Description("Z step considered a cliff (default 20).")] int cliffThreshold = 20,
        [Description("Max lines to return (default 100).")] int maxLines = 100)
    {
        foreach (var f in new[] { mapFilePath, staticsMulFilePath, staIdxMulFilePath, tileDataFilePath })
            if (!File.Exists(f)) return $"File not found: {f}";
        if (width < 1 || height < 1 || width > MaxRegion || height > MaxRegion)
            return $"Width/height must be 1-{MaxRegion}.";

        try
        {
            int rw = width + 1, rh = height + 1;
            var region = MapCoordinateReader.GetRegion(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, rw, rh, includeStatics: false);
            MapCoordinateInfo At(int dx, int dy) => region[dy * rw + dx];

            var texCache = new Dictionary<int, int>();
            int TexId(int id)
            {
                if (!texCache.TryGetValue(id, out var t))
                    texCache[id] = t = TileDataReader.GetLandDetail(tileDataFilePath, id).TextureId;
                return t;
            }

            var sb = new StringBuilder();
            int noTexSlope = 0, cliffs = 0, listed = 0;
            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    var t = At(dx, dy); var r = At(dx + 1, dy); var l = At(dx, dy + 1); var b = At(dx + 1, dy + 1);
                    int min = Math.Min(Math.Min(t.LandZ, r.LandZ), Math.Min(l.LandZ, b.LandZ));
                    int max = Math.Max(Math.Max(t.LandZ, r.LandZ), Math.Max(l.LandZ, b.LandZ));
                    if (min == max) continue;

                    bool noTex = TexId(t.LandTileId) == 0;
                    bool cliff = max - min >= cliffThreshold;
                    if (noTex) noTexSlope++;
                    if (cliff) cliffs++;
                    if (!noTex && !cliff) continue;
                    if (listed++ >= maxLines) continue;

                    sb.Append($"({t.X},{t.Y}) id=0x{t.LandTileId:X4} Z[{t.LandZ},{r.LandZ},{l.LandZ},{b.LandZ}] Δ{max - min}");
                    if (noTex) sb.Append(" NO-TEXTURE-ON-SLOPE");
                    if (cliff) sb.Append(" CLIFF");
                    sb.AppendLine();
                }
            }
            return $"Region ({x},{y}) {width}x{height}: sloped-without-texture={noTexSlope}, cliffs(>= {cliffThreshold})={cliffs}, listed={Math.Min(listed, maxLines)}/{listed}{Environment.NewLine}{sb}";
        }
        catch (Exception ex)
        {
            return $"Failed: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Diagnoses land transitions in a map region. For every land tile lists its four corner Z values (top=this, right=x+1, left=y+1, bottom=x+1,y+1), whether it is flat, slope/step size, and neighbor tile-ID differences (the places transition triangles are needed). Only non-flat tiles or tile-ID boundaries are listed. Optional tiledata path adds texture IDs.")]
    public string DiagnoseLandTransitions(
        [Description("Full path to mapN.mul or mapNLegacyMUL.uop.")] string mapFilePath,
        [Description("Full path to staticsN.mul.")] string staticsMulFilePath,
        [Description("Full path to staidxN.mul.")] string staIdxMulFilePath,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks,
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles (max 96).")] int width,
        [Description("Region height in tiles (max 96).")] int height,
        [Description("Optional full path to tiledata.mul for texture IDs/flags.")] string? tileDataFilePath = null,
        [Description("Max lines to return (default 200).")] int maxLines = 200)
    {
        foreach (var f in new[] { mapFilePath, staticsMulFilePath, staIdxMulFilePath })
            if (!File.Exists(f)) return $"File not found: {f}";
        if (width < 1 || height < 1 || width > MaxRegion || height > MaxRegion)
            return $"Width/height must be 1-{MaxRegion}.";

        try
        {
            // Read one extra row/column so edge tiles have right/left/bottom corners.
            int rw = width + 1, rh = height + 1;
            var region = MapCoordinateReader.GetRegion(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, rw, rh, includeStatics: false);
            MapCoordinateInfo At(int dx, int dy) => region[dy * rw + dx];

            var texCache = new Dictionary<int, string>();
            string Tex(int id)
            {
                if (tileDataFilePath is null || !File.Exists(tileDataFilePath)) return "";
                if (!texCache.TryGetValue(id, out var s))
                {
                    var d = TileDataReader.GetLandDetail(tileDataFilePath, id);
                    texCache[id] = s = d.TextureId != 0 ? $" tex={d.TextureId}" : "";
                }
                return s;
            }

            var sb = new StringBuilder();
            int listed = 0, flat = 0, sloped = 0, boundaries = 0;
            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    var t = At(dx, dy); var r = At(dx + 1, dy); var l = At(dx, dy + 1); var b = At(dx + 1, dy + 1);
                    bool isFlat = t.LandZ == r.LandZ && t.LandZ == l.LandZ && t.LandZ == b.LandZ;
                    bool idBoundary = t.LandTileId != r.LandTileId || t.LandTileId != l.LandTileId || t.LandTileId != b.LandTileId;
                    if (isFlat) flat++; else sloped++;
                    if (idBoundary) boundaries++;
                    if (isFlat && !idBoundary) continue;
                    if (listed++ >= maxLines) continue;

                    int minZ = Math.Min(Math.Min(t.LandZ, r.LandZ), Math.Min(l.LandZ, b.LandZ));
                    int maxZ = Math.Max(Math.Max(t.LandZ, r.LandZ), Math.Max(l.LandZ, b.LandZ));
                    sb.Append($"({t.X},{t.Y}) id=0x{t.LandTileId:X4}{Tex(t.LandTileId)} Z[top={t.LandZ} right={r.LandZ} left={l.LandZ} bottom={b.LandZ}] ")
                      .Append(isFlat ? "flat" : $"slope Δ{maxZ - minZ}");
                    if (idBoundary)
                        sb.Append($" | ids right=0x{r.LandTileId:X4} left=0x{l.LandTileId:X4} bottom=0x{b.LandTileId:X4}");
                    sb.AppendLine();
                }
            }

            var header = $"Region ({x},{y}) {width}x{height}: flat={flat}, sloped={sloped}, id-boundaries={boundaries}, listed={Math.Min(listed, maxLines)}/{listed}";
            return header + Environment.NewLine + sb;
        }
        catch (Exception ex)
        {
            return $"Failed to diagnose region: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Analyzes a map region and reports what information is still MISSING to fix transition problems: distinct land tiles (name, texture ID, art present, slope counts), sloped tiles lacking textures, Z-step patterns, tile-type boundary pairs, plus a checklist of questions only the user can answer.")]
    public string ReportMissingTransitionInfo(
        [Description("Full path to mapN.mul or mapNLegacyMUL.uop.")] string mapFilePath,
        [Description("Full path to staticsN.mul.")] string staticsMulFilePath,
        [Description("Full path to staidxN.mul.")] string staIdxMulFilePath,
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Full path to artidx.mul.")] string artIdxFilePath,
        [Description("Full path to art.mul.")] string artMulFilePath,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks,
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles (max 96).")] int width,
        [Description("Region height in tiles (max 96).")] int height)
    {
        foreach (var f in new[] { mapFilePath, staticsMulFilePath, staIdxMulFilePath, tileDataFilePath, artIdxFilePath, artMulFilePath })
            if (!File.Exists(f)) return $"File not found: {f}";
        if (width < 1 || height < 1 || width > MaxRegion || height > MaxRegion)
            return $"Width/height must be 1-{MaxRegion}.";

        try
        {
            int rw = width + 1, rh = height + 1;
            var region = MapCoordinateReader.GetRegion(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, rw, rh, includeStatics: false);
            MapCoordinateInfo At(int dx, int dy) => region[dy * rw + dx];

            var counts = new Dictionary<int, int>();
            var slopeCounts = new Dictionary<int, int>();
            var zSteps = new Dictionary<string, int>();
            var pairs = new Dictionary<(int, int), int>();

            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    var t = At(dx, dy); var r = At(dx + 1, dy); var l = At(dx, dy + 1); var b = At(dx + 1, dy + 1);
                    counts[t.LandTileId] = counts.GetValueOrDefault(t.LandTileId) + 1;
                    if (!(t.LandZ == r.LandZ && t.LandZ == l.LandZ && t.LandZ == b.LandZ))
                        slopeCounts[t.LandTileId] = slopeCounts.GetValueOrDefault(t.LandTileId) + 1;
                    if (t.LandZ != r.LandZ)
                    {
                        var k = $"{t.LandZ}->{r.LandZ}";
                        zSteps[k] = zSteps.GetValueOrDefault(k) + 1;
                    }
                    foreach (var n in new[] { r, l })
                        if (n.LandTileId != t.LandTileId)
                        {
                            var key = (Math.Min(t.LandTileId, n.LandTileId), Math.Max(t.LandTileId, n.LandTileId));
                            pairs[key] = pairs.GetValueOrDefault(key) + 1;
                        }
                }
            }

            var info = new Dictionary<int, (string Name, int Tex, bool HasArt)>();
            foreach (var id in counts.Keys.Concat(pairs.Keys.SelectMany(p => new[] { p.Item1, p.Item2 })).Distinct())
            {
                var d = TileDataReader.GetLandDetail(tileDataFilePath, id);
                var art = ArtReader.GetArtTileMul(artIdxFilePath, artMulFilePath, id);
                info[id] = (d.Name, d.TextureId, art is not null);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"== Region ({x},{y}) {width}x{height} ==");
            sb.AppendLine($"Distinct land tiles: {counts.Count}");
            foreach (var kv in counts.OrderByDescending(k => k.Value).Take(25))
            {
                var i = info[kv.Key];
                sb.AppendLine($"  0x{kv.Key:X4} \"{i.Name}\" tex={i.Tex} art={(i.HasArt ? "yes" : "NO")} count={kv.Value} sloped={slopeCounts.GetValueOrDefault(kv.Key)}");
            }

            sb.AppendLine("Most common horizontal Z steps (from->to: count):");
            foreach (var kv in zSteps.OrderByDescending(k => k.Value).Take(10))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");

            sb.AppendLine("Most common tile-type boundary pairs:");
            foreach (var kv in pairs.OrderByDescending(k => k.Value).Take(15))
                sb.AppendLine($"  0x{kv.Key.Item1:X4} | 0x{kv.Key.Item2:X4}: {kv.Value}");

            sb.AppendLine();
            sb.AppendLine("== MISSING / UNVERIFIED (measured from your files) ==");
            int n1 = 0;
            foreach (var kv in info.Where(k => !k.Value.HasArt))
                sb.AppendLine($"  [{++n1}] Tile 0x{kv.Key:X4} is used but has NO art in art.mul.");
            foreach (var kv in slopeCounts.Where(k => info[k.Key].Tex == 0))
                sb.AppendLine($"  [{++n1}] Tile 0x{kv.Key:X4} \"{info[kv.Key].Name}\" is sloped {kv.Value}x but has no texture ID, so it can only draw flat.");
            foreach (var kv in info.Where(k => string.IsNullOrWhiteSpace(k.Value.Name)))
                sb.AppendLine($"  [{++n1}] Tile 0x{kv.Key:X4} has no name in tiledata (purpose unknown).");
            if (n1 == 0) sb.AppendLine("  (none found)");

            sb.AppendLine();
            sb.AppendLine("== MISSING (only you can tell me) ==");
            sb.AppendLine("  Q1. Which exact coordinate looks broken, and what does the client draw there (gap, overlap, wrong colour, wrong edge)?");
            sb.AppendLine("  Q2. Are the Z terraces (e.g. the repeating step pattern above) intentional?");
            sb.AppendLine("  Q3. For each boundary pair above, which tile IDs are the intended transition pieces?");
            sb.AppendLine("  Q4. What does MapCreator do when it compiles overlapping tiles: which tile wins, and does it write Z for the transition tile?");
            sb.AppendLine("  Q5. Which client build is used, and does it draw sloped land from texmaps (texture ID) or flat art?");
            sb.AppendLine("  Q6. Is map4.mul in patches the file under test, and is MapCreator's source map available for comparison?");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed: {ex.Message}";
        }
    }
}

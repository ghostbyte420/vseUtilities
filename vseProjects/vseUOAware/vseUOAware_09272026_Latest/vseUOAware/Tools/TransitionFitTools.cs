using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Text-based "eyes" for an AI agent: edge-seam scoring of land tiles against
/// their four neighbours, candidate ranking, and ASCII previews.
/// Land art is a 44x44 diamond; edges are named by screen direction.
/// Neighbours: NE=(x,y-1), SE=(x+1,y), SW=(x,y+1), NW=(x-1,y).
/// </summary>
internal class TransitionFitTools
{
    private static readonly string[] EdgeNames = ["NE", "SE", "SW", "NW"];

    [McpServerTool]
    [Description("Scores how well a candidate LAND tile ID fits at a map coordinate by comparing its four diamond edges to the neighbouring land tiles' facing edges (mean RGB distance, lower = better seam). Neighbours come from the map. Use to decide if a transition piece is correct. Verdict is heuristic: <25 good, <50 fair, else poor.")]
    public string TestTransitionFit(
        [Description("Full path to mapN.mul or mapNLegacyMUL.uop.")] string mapFilePath,
        [Description("Full path to staticsN.mul.")] string staticsMulFilePath,
        [Description("Full path to staidxN.mul.")] string staIdxMulFilePath,
        [Description("Full path to artidx.mul.")] string artIdxFilePath,
        [Description("Full path to art.mul.")] string artMulFilePath,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks,
        [Description("Tile X.")] int x,
        [Description("Tile Y.")] int y,
        [Description("Candidate land tile ID (0-0x3FFF) to test at this coordinate.")] int candidateTileId)
    {
        var err = Validate(mapFilePath, staticsMulFilePath, staIdxMulFilePath, artIdxFilePath, artMulFilePath);
        if (err is not null) return err;
        try
        {
            var cache = new Dictionary<int, ArtImage?>();
            var neighbours = ReadNeighbours(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y);
            var sb = new StringBuilder();
            var (score, detail) = Score(candidateTileId, neighbours, artIdxFilePath, artMulFilePath, cache);
            sb.AppendLine($"Candidate 0x{candidateTileId:X4} at ({x},{y}): score={score:F1} -> {Verdict(score)}");
            sb.Append(detail);
            return sb.ToString();
        }
        catch (Exception ex) { return $"Failed: {ex.Message}"; }
    }

    [McpServerTool]
    [Description("Ranks many candidate land tile IDs for a map coordinate by edge-seam fit against its four neighbours (best first). candidates is a comma list of IDs and ranges, decimal or 0x hex, e.g. '0x1A-0x30,100,0x2AB'. Includes the tile currently on the map for comparison.")]
    public string RankTransitionCandidates(
        [Description("Full path to mapN.mul or mapNLegacyMUL.uop.")] string mapFilePath,
        [Description("Full path to staticsN.mul.")] string staticsMulFilePath,
        [Description("Full path to staidxN.mul.")] string staIdxMulFilePath,
        [Description("Full path to artidx.mul.")] string artIdxFilePath,
        [Description("Full path to art.mul.")] string artMulFilePath,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks,
        [Description("Tile X.")] int x,
        [Description("Tile Y.")] int y,
        [Description("Candidate IDs/ranges, e.g. '0x1A-0x30,100'.")] string candidates,
        [Description("How many top results to return (default 10).")] int top = 10)
    {
        var err = Validate(mapFilePath, staticsMulFilePath, staIdxMulFilePath, artIdxFilePath, artMulFilePath);
        if (err is not null) return err;
        try
        {
            var ids = ParseIds(candidates);
            if (ids.Count == 0) return "No valid candidate IDs.";
            if (ids.Count > 4000) return "Too many candidates (max 4000).";

            var cache = new Dictionary<int, ArtImage?>();
            var neighbours = ReadNeighbours(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y);
            int current = MapCoordinateReader.GetCoordinate(mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, x, y, false).LandTileId;
            if (!ids.Contains(current)) ids.Add(current);

            var ranked = ids
                .Select(id => (Id: id, Score: Score(id, neighbours, artIdxFilePath, artMulFilePath, cache).Score))
                .OrderBy(r => r.Score).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"({x},{y}) neighbours NE/SE/SW/NW = {string.Join(" ", neighbours.Select(n => $"0x{n:X4}"))}; current=0x{current:X4}");
            foreach (var r in ranked.Take(top))
                sb.AppendLine($"0x{r.Id:X4}  score={r.Score:F1}  {Verdict(r.Score)}{(r.Id == current ? "  <- current" : "")}");
            int rank = ranked.FindIndex(r => r.Id == current) + 1;
            sb.AppendLine($"Current tile ranks {rank} of {ranked.Count}.");
            return sb.ToString();
        }
        catch (Exception ex) { return $"Failed: {ex.Message}"; }
    }

    [McpServerTool]
    [Description("Returns a text preview of one art tile from art.mul/artidx.mul: a luminance ASCII picture (downsampled 2x horizontally), dominant average color, and for land tiles the average color of each diamond edge (NE/SE/SW/NW). Lets an AI 'see' a tile without image support.")]
    public string DescribeArtTileMul(
        [Description("Full path to artidx.mul.")] string artIdxFilePath,
        [Description("Full path to art.mul.")] string artMulFilePath,
        [Description("Tile ID (land 0-0x3FFF, items 0x4000+).")] int tileId)
    {
        if (!File.Exists(artIdxFilePath)) return $"File not found: {artIdxFilePath}";
        if (!File.Exists(artMulFilePath)) return $"File not found: {artMulFilePath}";
        try
        {
            var img = ArtReader.GetArtTileMul(artIdxFilePath, artMulFilePath, tileId);
            if (img is null) return $"No art for tile {tileId}.";

            const string ramp = " .:-=+*#%@";
            var sb = new StringBuilder();
            sb.AppendLine($"Tile 0x{tileId:X4} {img.Width}x{img.Height}");
            for (int py = 0; py < img.Height; py += 2)
            {
                for (int px = 0; px < img.Width; px++)
                {
                    int i = (py * img.Width + px) * 4;
                    if (img.Rgba[i + 3] == 0) { sb.Append(' '); continue; }
                    double lum = (0.299 * img.Rgba[i] + 0.587 * img.Rgba[i + 1] + 0.114 * img.Rgba[i + 2]) / 255.0;
                    sb.Append(ramp[Math.Clamp((int)(lum * (ramp.Length - 1) + 0.5), 1, ramp.Length - 1)]);
                }
                sb.AppendLine();
            }

            var avg = Average(img, 0, img.Height, 0, img.Width);
            sb.AppendLine($"Average color: {Hex(avg)}");
            if (tileId < 0x4000 && img.Width == 44 && img.Height == 44)
                for (int e = 0; e < 4; e++)
                    sb.AppendLine($"Edge {EdgeNames[e]}: {Hex(EdgeAverage(img, e))}");
            return sb.ToString();
        }
        catch (Exception ex) { return $"Failed: {ex.Message}"; }
    }

    // ---- scoring -------------------------------------------------------

    private static (double Score, string Detail) Score(int candidate, int[] neighbours, string idx, string mul, Dictionary<int, ArtImage?> cache)
    {
        ArtImage? Get(int id)
        {
            if (!cache.TryGetValue(id, out var im))
                cache[id] = im = ArtReader.GetArtTileMul(idx, mul, id);
            return im;
        }

        var me = Get(candidate);
        if (me is null || me.Width != 44 || me.Height != 44)
            return (999, "Candidate has no 44x44 land art.\n");

        var sb = new StringBuilder();
        double total = 0; int used = 0;
        for (int e = 0; e < 4; e++)
        {
            var nb = Get(neighbours[e]);
            if (nb is null || nb.Width != 44 || nb.Height != 44) { sb.AppendLine($"  {EdgeNames[e]} vs 0x{neighbours[e]:X4}: neighbour has no land art"); continue; }
            double d = EdgeDistance(me, nb, e);
            sb.AppendLine($"  {EdgeNames[e]} vs 0x{neighbours[e]:X4}: dist={d:F1}");
            total += d; used++;
        }
        return (used == 0 ? 999 : total / used, sb.ToString());
    }

    /// <summary>Mean RGB distance along one edge between my tile and the neighbour's facing edge.</summary>
    private static double EdgeDistance(ArtImage me, ArtImage nb, int edge)
    {
        double sum = 0; int n = 0;
        for (int k = 0; k < 22; k++)
        {
            int mr, mx, nr, nx;
            switch (edge)
            {
                case 0: mr = k;      mx = 22 + k;  nr = k + 22; nx = k;       break; // NE vs neighbour SW
                case 1: mr = 22 + k; mx = 43 - k;  nr = k;      nx = 21 - k;  break; // SE vs neighbour NW
                case 2: mr = 22 + k; mx = k;       nr = k;      nx = 22 + k;  break; // SW vs neighbour NE
                default: mr = k;     mx = 21 - k;  nr = 22 + k; nx = 43 - k;  break; // NW vs neighbour SE
            }
            int a = (mr * 44 + mx) * 4, b = (nr * 44 + nx) * 4;
            if (me.Rgba[a + 3] == 0 || nb.Rgba[b + 3] == 0) continue;
            double dr = me.Rgba[a] - nb.Rgba[b], dg = me.Rgba[a + 1] - nb.Rgba[b + 1], db = me.Rgba[a + 2] - nb.Rgba[b + 2];
            sum += Math.Sqrt(dr * dr + dg * dg + db * db) / 1.732; // normalise to 0-255
            n++;
        }
        return n == 0 ? 255 : sum / n;
    }

    private static (int R, int G, int B) EdgeAverage(ArtImage img, int edge)
    {
        long r = 0, g = 0, b = 0; int n = 0;
        for (int k = 0; k < 22; k++)
        {
            int row, x;
            switch (edge)
            {
                case 0: row = k; x = 22 + k; break;
                case 1: row = 22 + k; x = 43 - k; break;
                case 2: row = 22 + k; x = k; break;
                default: row = k; x = 21 - k; break;
            }
            int i = (row * 44 + x) * 4;
            if (img.Rgba[i + 3] == 0) continue;
            r += img.Rgba[i]; g += img.Rgba[i + 1]; b += img.Rgba[i + 2]; n++;
        }
        return n == 0 ? (0, 0, 0) : ((int)(r / n), (int)(g / n), (int)(b / n));
    }

    private static (int R, int G, int B) Average(ArtImage img, int y0, int y1, int x0, int x1)
    {
        long r = 0, g = 0, b = 0; int n = 0;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                int i = (y * img.Width + x) * 4;
                if (img.Rgba[i + 3] == 0) continue;
                r += img.Rgba[i]; g += img.Rgba[i + 1]; b += img.Rgba[i + 2]; n++;
            }
        return n == 0 ? (0, 0, 0) : ((int)(r / n), (int)(g / n), (int)(b / n));
    }

    private static string Hex((int R, int G, int B) c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    private static string Verdict(double s) => s < 25 ? "GOOD" : s < 50 ? "FAIR" : "POOR";

    // ---- helpers -------------------------------------------------------

    private static int[] ReadNeighbours(string map, string statics, string staidx, int fw, int x, int y)
    {
        int Id(int nx, int ny) => nx < 0 || ny < 0 ? 0
            : MapCoordinateReader.GetCoordinate(map, statics, staidx, fw, nx, ny, false).LandTileId;
        return [Id(x, y - 1), Id(x + 1, y), Id(x, y + 1), Id(x - 1, y)];
    }

    private static string? Validate(params string[] files)
    {
        foreach (var f in files)
            if (!File.Exists(f)) return $"File not found: {f}";
        return null;
    }

    private static List<int> ParseIds(string text)
    {
        var result = new List<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = part.Split('-', 2, StringSplitOptions.TrimEntries);
            if (!TryNum(range[0], out int a)) continue;
            int b = a;
            if (range.Length == 2 && !TryNum(range[1], out b)) continue;
            for (int i = Math.Min(a, b); i <= Math.Max(a, b) && result.Count <= 4000; i++)
                if (i is >= 0 and < 0x4000 && !result.Contains(i)) result.Add(i);
        }
        return result;
    }

    private static bool TryNum(string s, out int v) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(s.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out v)
            : int.TryParse(s, out v);
}

namespace vseUOAware.Services;

/// <summary>
/// Turns a facet (map) number into the map, statics and index file paths inside
/// the saved UO folder, plus the facet size in 8x8 blocks.
/// Facets 0-5 are the defaults with known sizes. Any other number (Map6, Map32, Map10000...)
/// is a custom map, limited only by the files that exist, never by the client.
/// For flat mapN.mul files the size is checked against the file length (196 bytes per block).
/// </summary>
internal static class FacetResolver
{
    internal sealed record FacetPaths(
        int Facet,
        string MapFile,
        string StaticsFile,
        string StaIdxFile,
        int WidthBlocks,
        int HeightBlocks);

    // Known sizes for the default facets 0-5, newest first. A flat mapN.mul is matched to one of these by its file length.
    private static readonly (int Width, int Height)[][] DefaultSizes =
    [
        [(896, 512), (768, 512)], // 0 Felucca (NEW, OLD)
        [(896, 512), (768, 512)], // 1 Trammel (NEW, OLD)
        [(288, 200)],             // 2 Ilshenar
        [(320, 256)],             // 3 Malas
        [(181, 181)],             // 4 Tokuno
        [(160, 512)],             // 5 Ter Mur
    ];

    private static (int Width, int Height)[] KnownSizes(int facet) =>
        facet < DefaultSizes.Length ? DefaultSizes[facet] : [];

    private const int MapBlockBytes = 196;
    private const int MaxCustomBlocks = 1250;

    /// <summary>
    /// Converts a width given in tiles (= paint-program pixels) to 8x8 blocks. Map sizes must be
    /// divisible by 8 or the client crashes, so anything else is rejected with a clear message.
    /// Does nothing when tiles is 0 (not given).
    /// </summary>
    public static bool TryTilesToBlocks(int widthTiles, ref int widthBlocks, out string error)
    {
        error = string.Empty;
        if (widthTiles == 0)
            return true;
        if (widthTiles < 0 || widthTiles % 8 != 0)
        {
            error = $"{widthTiles} tiles is not divisible by 8. Map sizes must be divisible by 8 (the nearest are {widthTiles / 8 * 8} and {(widthTiles / 8 + 1) * 8}).";
            return false;
        }
        if (widthTiles > MaxCustomBlocks * 8)
        {
            error = $"{widthTiles} tiles is larger than the maximum of {MaxCustomBlocks * 8} tiles.";
            return false;
        }
        int blocks = widthTiles / 8;
        if (widthBlocks > 0 && widthBlocks != blocks)
        {
            error = $"facetWidthBlocks ({widthBlocks}) and facetWidthTiles ({widthTiles} = {blocks} blocks) disagree. Give only one.";
            return false;
        }
        widthBlocks = blocks;
        return true;
    }

    /// <param name="widthOverrideBlocks">Width in blocks for custom-size maps; height is then worked out from the file length.</param>
    /// <param name="heightOverrideBlocks">Optional height in blocks; when given with the width both are checked against the file.</param>
    public static bool TryResolve(int facet, string? uoDirectory, int? widthOverrideBlocks, out FacetPaths paths, out string error, int? heightOverrideBlocks = null)
    {
        paths = null!;
        error = string.Empty;

        if (facet < 0)
        {
            error = "Facet must be 0 or higher.";
            return false;
        }

        var dir = string.IsNullOrWhiteSpace(uoDirectory) ? DirectoryService.LoadSavedDirectory() : uoDirectory;
        if (string.IsNullOrWhiteSpace(dir))
        {
            error = "No UO folder is saved. Call set_uo_directory first, or pass the file paths directly.";
            return false;
        }
        if (!Directory.Exists(dir))
        {
            error = $"UO folder not found: {dir}";
            return false;
        }

        var uop = Path.Combine(dir, $"map{facet}LegacyMUL.uop");
        var mul = Path.Combine(dir, $"map{facet}.mul");
        var map = File.Exists(uop) ? uop : mul;
        var statics = Path.Combine(dir, $"statics{facet}.mul");
        var staIdx = Path.Combine(dir, $"staidx{facet}.mul");

        foreach (var f in new[] { map, statics, staIdx })
        {
            if (!File.Exists(f))
            {
                error = $"File not found: {f}";
                return false;
            }
        }

        int w, h;
        var known = KnownSizes(facet);

        long blocks;
        try
        {
            blocks = MapCoordinateReader.CountBlocks(map);
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }

        if (widthOverrideBlocks is > 0 && heightOverrideBlocks is > 0)
        {
            w = widthOverrideBlocks.Value;
            h = heightOverrideBlocks.Value;
            if ((long)w * h != blocks)
            {
                error = $"{Path.GetFileName(map)} holds {blocks} blocks, but {w} x {h} blocks ({w * 8} x {h * 8} tiles) = {(long)w * h} blocks. The size you gave does not match the file.";
                return false;
            }
        }
        else if (heightOverrideBlocks is > 0)
        {
            h = heightOverrideBlocks.Value;
            if (blocks % h != 0)
            {
                error = $"{Path.GetFileName(map)} holds {blocks} blocks, which does not divide evenly by height {h} blocks ({h * 8} tiles). The height is wrong.";
                return false;
            }
            w = (int)(blocks / h);
        }
        else if (widthOverrideBlocks is > 0)
        {
            w = widthOverrideBlocks.Value;
            if (blocks % w != 0)
            {
                error = $"{Path.GetFileName(map)} holds {blocks} blocks, which does not divide evenly by width {w} blocks ({w * 8} tiles). The width is wrong.";
                return false;
            }
            h = (int)(blocks / w);
        }
        else
        {
            var match = known.Where(s => (long)s.Width * s.Height == blocks).Select(s => ((int, int)?)s).FirstOrDefault();
            if (match is null)
            {
                var fits = new List<(int W, int H)>();
                for (int cw = 1; cw <= MaxCustomBlocks; cw++)
                    if (blocks % cw == 0 && blocks / cw <= MaxCustomBlocks)
                        fits.Add((cw, (int)(blocks / cw)));

                if (fits.Count == 0)
                {
                    error = $"{Path.GetFileName(map)} holds {blocks} blocks, which cannot form any map of at most {MaxCustomBlocks} x {MaxCustomBlocks} blocks ({MaxCustomBlocks * 8} x {MaxCustomBlocks * 8} tiles). The file is not a valid map size.";
                    return false;
                }
                if (fits.Count > 1)
                {
                    var list = string.Join("; ", fits.Take(12).Select(f => $"{f.W} x {f.H} blocks = {f.W * 8} x {f.H * 8} tiles"));
                    error = $"{Path.GetFileName(map)} holds {blocks} blocks and {fits.Count} valid sizes fit it (e.g. {list}). QUESTION: is this a legacy map (Map0-5) or a custom map? If custom, call again with facetWidthTiles and facetHeightTiles (e.g. Trammel size = 7168 x 4096).";
                    return false;
                }
                (w, h) = fits[0];
            }
            else
            {
                (w, h) = match.Value;
            }
        }

        paths = new FacetPaths(facet, map, statics, staIdx, w, h);
        return true;
    }
}

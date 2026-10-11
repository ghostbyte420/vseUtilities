using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

/// <summary>
/// Read-only export of a map region to a PNG, one pixel per tile, coloured from radarcol.mul.
/// </summary>
internal class MapExportTools
{
    private const int MaxSide = 1024;
    private const int ChunkSize = 64;

    [McpServerTool]
    [Description("Exports a map region (max 1024x1024 tiles) to a PNG, one pixel per tile, using radarcol.mul colours (land tile ID, or the highest static if includeStatics is true). Returns the saved file path. Read-only.")]
    public string ExportMapRegionPng(
        [Description("Region start X.")] int x,
        [Description("Region start Y.")] int y,
        [Description("Region width in tiles (max 1024).")] int width,
        [Description("Region height in tiles (max 1024).")] int height,
        [Description("Full path to radarcol.mul.")] string radarColFilePath,
        [Description("Facet number (0-5 defaults, higher = custom map).")] int? facet = null,
        [Description("Full path to the map file. Not needed when facet is given.")] string? mapFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8).")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8).")] int facetHeightTiles = 0,
        [Description("Draw the highest static of each tile over the land colour (default true).")] bool includeStatics = true,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/Map/map_<x>_<y>_<w>x<h>.png'.")] string? outputPngPath = null)
    {
        if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide) return $"Region must be positive and at most {MaxSide}x{MaxSide} tiles.";
        if (!File.Exists(radarColFilePath)) return "radarcol.mul not found: " + radarColFilePath;
        if (!MapAnalysisTools.Resolve(facet, mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, includeStatics,
                out var map, out var sta, out var idx, out var wb, out var err))
            return err;
        try
        {
            var raw = File.ReadAllBytes(radarColFilePath);
            int entries = raw.Length / 2;
            var palette = new (byte R, byte G, byte B)[entries];
            for (int i = 0; i < entries; i++)
            {
                ushort v = (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                palette[i] = ((byte)(((v >> 10) & 0x1F) * 255 / 31), (byte)(((v >> 5) & 0x1F) * 255 / 31), (byte)((v & 0x1F) * 255 / 31));
            }

            var rgba = new byte[width * height * 4];
            for (int cy = y; cy < y + height; cy += ChunkSize)
                for (int cx = x; cx < x + width; cx += ChunkSize)
                {
                    int w = Math.Min(ChunkSize, x + width - cx), h = Math.Min(ChunkSize, y + height - cy);
                    foreach (var t in MapCoordinateReader.GetRegion(map, sta, idx, wb, cx, cy, w, h, includeStatics))
                    {
                        int colorIndex = t.LandTileId;
                        if (includeStatics && t.Statics.Count > 0)
                        {
                            var top = t.Statics[0];
                            foreach (var s in t.Statics) if (s.Z >= top.Z) top = s;
                            colorIndex = 0x4000 + top.TileId;
                        }
                        if (colorIndex >= entries) colorIndex = t.LandTileId;
                        var c = colorIndex < entries ? palette[colorIndex] : default;
                        int o = ((t.Y - y) * width + (t.X - x)) * 4;
                        rgba[o] = c.R; rgba[o + 1] = c.G; rgba[o + 2] = c.B; rgba[o + 3] = 255;
                    }
                }

            string path = ResolveOutput(outputPngPath, $"map_{x}_{y}_{width}x{height}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            PngImageWriter.SaveRgbaAsPng(rgba, width, height, path);
            return $"Saved {width}x{height} PNG ({(includeStatics ? "land + statics" : "land only")}): {path}";
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    private static string ResolveOutput(string? requested, string defaultName)
    {
        string baseDir = Path.Combine(AppContext.BaseDirectory, "uoaExport", "Map");
        if (string.IsNullOrWhiteSpace(requested)) return Path.Combine(baseDir, defaultName);
        return Path.IsPathRooted(requested) ? requested : Path.Combine(baseDir, requested);
    }
}

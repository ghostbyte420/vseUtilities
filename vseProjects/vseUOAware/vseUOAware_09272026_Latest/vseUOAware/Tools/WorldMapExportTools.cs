using ModelContextProtocol.Server;
using System.ComponentModel;
using vseUOAware.Services;

/// <summary>
/// Reproduces the uoAvox client's WorldMapGump.Load() image (radarcol land + statics that pass
/// GameObject.CanBeDrawn, hued statics, Z-based shading, 2px padded texture).
/// </summary>
internal class WorldMapExportTools
{
    private const int OffsetPix = 2;
    private const int OffsetHalf = 1;
    private const int Chunk = 64;

    [McpServerTool(Name = "export_client_world_map")]
    [Description("Exports the whole facet as the same PNG the uoAvox game client's world map view renders (radarcol land, drawable statics, hued statics, Z shading, 2px padded texture). Read-only. Large maps take a while.")]
    public static string ExportClientWorldMap(
        [Description("Full path to radarcol.mul.")] string radarColFilePath,
        [Description("Full path to hues.mul.")] string huesFilePath,
        [Description("Full path to tiledata.mul.")] string tileDataFilePath,
        [Description("Facet number (0-5 defaults, higher = custom map).")] int? facet = null,
        [Description("Full path to the map file. Not needed when facet is given.")] string? mapFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8).")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8).")] int facetHeightTiles = 0,
        [Description("Full path, or bare filename, for the output PNG. Defaults to 'uoaExport/Map/clientworldmap.png'.")] string? outputPngPath = null)
    {
        foreach (var f in new[] { radarColFilePath, huesFilePath, tileDataFilePath })
            if (!File.Exists(f)) return "File not found: " + f;
        if (!MapAnalysisTools.Resolve(facet, mapFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, true,
                out var map, out var sta, out var idx, out var wb, out var err))
            return err;
        try
        {
            int realW = wb * 8;
            int realH = MapCoordinateReader.ResolveHeightBlocks(map, wb) * 8;
            int stride = realW + OffsetPix;
            int texH = realH + OffsetPix;

            var radar = ReadUShorts(radarColFilePath);
            var hueRaw = File.ReadAllBytes(huesFilePath);
            int hueCount = hueRaw.Length / 708 * 8;
            var drawCache = new Dictionary<int, bool>();

            var buffer = new uint[stride * texH];
            var allZ = new sbyte[stride * texH];

            for (int cy = 0; cy < realH; cy += Chunk)
                for (int cx = 0; cx < realW; cx += Chunk)
                {
                    int w = Math.Min(Chunk, realW - cx), h = Math.Min(Chunk, realH - cy);
                    foreach (var t in MapCoordinateReader.GetRegion(map, sta, idx, wb, cx, cy, w, h, true))
                    {
                        int block = (t.Y + OffsetHalf) * stride + t.X + OffsetHalf;
                        buffer[block] = ToArgb(Radar(radar, t.LandTileId & 0x3FFF));
                        allZ[block] = (sbyte)t.LandZ;
                        foreach (var s in t.Statics)
                        {
                            if (s.TileId == 0 || s.TileId == 0xFFFF || !CanBeDrawn(tileDataFilePath, s.TileId, drawCache)) continue;
                            if (s.Z < allZ[block]) continue;
                            ushort color = s.Hue != 0 ? HueColor16(hueRaw, hueCount, s.Hue) : Radar(radar, s.TileId + 0x4000);
                            buffer[block] = ToArgb(color);
                            allZ[block] = (sbyte)s.Z;
                        }
                    }
                }

            const float Mag0 = 80f / 100f, Mag1 = 100f / 80f;
            for (int mapY = 1; mapY < realH - 1; mapY++)
            {
                int cur = (mapY + OffsetHalf) * stride + OffsetHalf;
                int next = (mapY + 1 + OffsetHalf) * stride + OffsetHalf;
                for (int mapX = 1; mapX < realW - 1; mapX++)
                {
                    sbyte z0 = allZ[++cur];
                    sbyte z1 = allZ[next++];
                    if (z0 == z1) continue;
                    uint cc = buffer[cur];
                    if (cc == 0) continue;
                    byte r = (byte)(cc & 0xFF), g = (byte)((cc >> 8) & 0xFF), b = (byte)((cc >> 16) & 0xFF), a = (byte)((cc >> 24) & 0xFF);
                    if (r == 0 && g == 0 && b == 0) continue;
                    float m = z0 < z1 ? Mag0 : Mag1;
                    r = (byte)Math.Min(0xFF, r * m); g = (byte)Math.Min(0xFF, g * m); b = (byte)Math.Min(0xFF, b * m);
                    buffer[cur] = (uint)(r | (g << 8) | (b << 16) | (a << 24));
                }
            }

            var rgba = new byte[buffer.Length * 4];
            Buffer.BlockCopy(buffer, 0, rgba, 0, rgba.Length);

            string path = ExportPathResolver.Resolve(outputPngPath, "clientworldmap.png", "Map");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            PngImageWriter.SaveRgbaAsPng(rgba, stride, texH, path);
            return $"Saved client world map {stride}x{texH} ({realW}x{realH} map + {OffsetPix}px padding): {path}";
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    private static readonly byte[] Table =
    {
        0x00, 0x08, 0x10, 0x18, 0x20, 0x29, 0x31, 0x39, 0x41, 0x4A, 0x52, 0x5A, 0x62, 0x6A, 0x73, 0x7B, 0x83, 0x8B,
        0x94, 0x9C, 0xA4, 0xAC, 0xB4, 0xBD, 0xC5, 0xCD, 0xD5, 0xDE, 0xE6, 0xEE, 0xF6, 0xFF
    };

    private static uint ToArgb(ushort c16)
    {
        ushort c = (ushort)(0x8000 | c16);
        return (uint)(Table[(c >> 10) & 0x1F] | (Table[(c >> 5) & 0x1F] << 8) | (Table[c & 0x1F] << 16)) | 0xFF000000;
    }

    private static ushort Radar(ushort[] radar, int i) => i >= 0 && i < radar.Length ? radar[i] : (ushort)0;

    private static ushort HueColor16(byte[] hues, int hueCount, int hue)
    {
        const ushort c = 16384;
        if (hue != 0 && hue < hueCount)
        {
            int h = hue - 1;
            long off = (long)(h >> 3) * 708 + 4 + (h % 8) * 88 + ((c >> 10) & 0x1F) * 2;
            return (ushort)(hues[off] | (hues[off + 1] << 8));
        }
        return c;
    }

    private static ushort[] ReadUShorts(string path)
    {
        var raw = File.ReadAllBytes(path);
        var r = new ushort[raw.Length / 2];
        Buffer.BlockCopy(raw, 0, r, 0, r.Length * 2);
        return r;
    }

    private static bool CanBeDrawn(string tileData, int g, Dictionary<int, bool> cache)
    {
        if (cache.TryGetValue(g, out var v)) return v;
        v = Compute(tileData, g);
        cache[g] = v;
        return v;
    }

    private static bool Compute(string tileData, int g)
    {
        switch (g)
        {
            case 0x0001: case 0x21BC: case 0xA1FE: case 0xA1FF: case 0xA200: case 0xA201: return false;
            case 0x9E4C: case 0x9E64: case 0x9E65: case 0x9E7D:
                var d = Flags(tileData, g);
                return d.HasValue && !d.Value.HasFlag(TileFlag.Background) && !d.Value.HasFlag(TileFlag.Surface);
        }
        if (g == 0x63D3) return false;
        if (g >= 0x2198 && g <= 0x21A4) return false;
        var f = Flags(tileData, g);
        return f.HasValue && !f.Value.HasFlag(TileFlag.NoDiagonal);
    }

    private static TileFlag? Flags(string tileData, int g)
    {
        try { return TileDataReader.GetItemDetail(tileData, g + 0x4000).Flags; }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}

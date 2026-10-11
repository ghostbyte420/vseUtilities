namespace vseUOAware.Services;

/// <summary>
/// Reads facet0N.mul: a per-facet black/white overview bitmap in the same
/// family as Multimap.rle, but using a different per-row encoding. Format:
/// 2-byte width, 2-byte height, then for each row a 4-byte byte-count of the
/// row's encoded data followed by that many bytes of (1-byte run length,
/// 2-byte BGR555-style color) pairs, XORed with 0x8000 to flip the "black"
/// high bit convention used by the client's 16bpp ARGB1555 surface.
/// </summary>
internal static class FacetMapReader
{
    public static (byte[] Rgba, int Width, int Height)? GetFacetImage(string facetMulFilePath)
    {
        if (!File.Exists(facetMulFilePath))
            return null;

        using var stream = File.OpenRead(facetMulFilePath);
        using var reader = new BinaryReader(stream);

        int width = reader.ReadInt16();
        int height = reader.ReadInt16();
        if (width <= 0 || height <= 0)
            return null;

        var rgba = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            if (stream.Position >= stream.Length)
                break;

            int rowByteCount = reader.ReadInt32();
            int colorsCount = rowByteCount / 3;
            long rowEnd = stream.Position + rowByteCount;

            int x = 0;
            for (int c = 0; c < colorsCount && x < width; c++)
            {
                if (stream.Position >= stream.Length)
                    break;

                byte count = reader.ReadByte();
                short color = reader.ReadInt16();
                ushort argb1555 = (ushort)(color ^ 0x8000);

                bool black = (argb1555 & 0x8000) != 0;
                byte gray = black ? (byte)0 : (byte)255;

                int end = Math.Min(x + count, width);
                for (; x < end; x++)
                {
                    int o = (y * width + x) * 4;
                    rgba[o] = gray;
                    rgba[o + 1] = gray;
                    rgba[o + 2] = gray;
                    rgba[o + 3] = 255;
                }
            }

            // Advance to the exact start of the next row, in case of any
            // trailing/partial data not fully consumed above.
            if (rowEnd <= stream.Length)
                stream.Position = rowEnd;
        }

        return (rgba, width, height);
    }
}

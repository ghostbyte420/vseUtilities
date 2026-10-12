namespace vseUOAware.Services;

/// <summary>Minimal reader for uncompressed 8-bit indexed BMP files; returns the raw palette index per pixel (top-down).</summary>
internal static class BmpIndexReader
{
    public static bool TryRead(string path, out byte[] indices, out int width, out int height, out string error)
    {
        indices = Array.Empty<byte>(); width = height = 0; error = "";
        try
        {
            byte[] d = File.ReadAllBytes(path);
            if (d.Length < 54 || d[0] != 'B' || d[1] != 'M') { error = "Not a BMP file."; return false; }
            int dataOffset = BitConverter.ToInt32(d, 10);
            int dibSize = BitConverter.ToInt32(d, 14);
            if (dibSize < 40) { error = "Unsupported BMP header (old OS/2 format)."; return false; }
            width = BitConverter.ToInt32(d, 18);
            int h = BitConverter.ToInt32(d, 22);
            short bpp = BitConverter.ToInt16(d, 28);
            int compression = BitConverter.ToInt32(d, 30);
            if (bpp != 8) { error = $"BMP is {bpp}-bit; MapCreator needs 8-bit indexed (pixel index = terrain/altitude ID)."; return false; }
            if (compression != 0) { error = "Compressed (RLE) BMP is not supported."; return false; }
            bool bottomUp = h > 0; height = Math.Abs(h);
            if (width <= 0 || height == 0) { error = "Invalid BMP size."; return false; }
            int stride = (width + 3) & ~3;
            if ((long)dataOffset + (long)stride * height > d.Length) { error = "BMP pixel data is truncated."; return false; }
            indices = new byte[(long)width * height];
            for (int row = 0; row < height; row++)
            {
                int srcRow = bottomUp ? height - 1 - row : row;
                Buffer.BlockCopy(d, dataOffset + srcRow * stride, indices, row * width, width);
            }
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
}

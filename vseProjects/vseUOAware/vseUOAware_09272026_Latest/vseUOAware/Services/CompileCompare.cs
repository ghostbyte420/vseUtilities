using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace vseUOAware.Services;

/// <summary>
/// Tool 5: compares the files two compile runs produced (a "before" folder and an "after" folder).
/// It does not run the compiler. For each file it says: same, different, or only in one folder.
/// For map files (map0.mul, map1.mul ...) it also counts which blocks and tiles changed. Read-only.
/// </summary>
internal static class CompileCompare
{
    private const int BlockBytes = 196;
    private const int HeaderBytes = 4;
    private const int CellsPerBlock = 64;
    private const int CellBytes = 3;
    private const int BlockSide = 8;
    private const int MaxFileLines = 200;

    private static readonly Regex MapNameRx = new(@"^map\d+\.mul$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Run(string beforeFolder, string afterFolder, int maxResults, int facetHeightBlocks)
    {
        if (!Directory.Exists(beforeFolder)) return "FAILED: Before folder not found: " + beforeFolder;
        if (!Directory.Exists(afterFolder)) return "FAILED: After folder not found: " + afterFolder;
        if (string.Equals(Path.GetFullPath(beforeFolder), Path.GetFullPath(afterFolder), StringComparison.OrdinalIgnoreCase))
            return "FAILED: Both folders are the same folder. Give two different compile outputs.";
        if (maxResults < 1 || maxResults > 1000) return "FAILED: maxResults must be from 1 to 1000.";
        if (facetHeightBlocks < 0) return "FAILED: facetHeightBlocks cannot be negative. Use 0 if you do not know it.";

        var before = ListFiles(beforeFolder);
        var after = ListFiles(afterFolder);
        var names = before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

        var sb = new StringBuilder();
        SourceScan.Add(sb, $"# Compile output comparison");
        SourceScan.Add(sb, $"Before: {beforeFolder}");
        SourceScan.Add(sb, $"After:  {afterFolder}");
        sb.AppendLine("Nothing was changed. This only reads and compares.");
        sb.AppendLine();

        int same = 0, different = 0, onlyOne = 0, shown = 0;
        var mapNotes = new StringBuilder();
        foreach (var name in names)
        {
            bool inBefore = before.TryGetValue(name, out var pathB);
            bool inAfter = after.TryGetValue(name, out var pathA);
            string line;
            if (!inBefore || !inAfter) { onlyOne++; line = $"- {name}: only in {(inBefore ? "BEFORE" : "AFTER")}"; }
            else if (Hash(pathB!) == Hash(pathA!)) { same++; line = $"- {name}: same"; }
            else
            {
                different++;
                line = $"- {name}: DIFFERENT (before {new FileInfo(pathB!).Length} bytes, after {new FileInfo(pathA!).Length} bytes)";
                if (MapNameRx.IsMatch(Path.GetFileName(name))) mapNotes.Append(CompareMap(name, pathB!, pathA!, maxResults, facetHeightBlocks));
            }
            if (shown++ < MaxFileLines) sb.AppendLine(line);
        }
        if (names.Count > MaxFileLines) SourceScan.Add(sb, $"(+{names.Count - MaxFileLines} more files not listed)");
        sb.AppendLine();
        SourceScan.Add(sb, $"Summary: {same} same, {different} different, {onlyOne} only in one folder.");
        if (mapNotes.Length > 0) { sb.AppendLine(); sb.Append(mapNotes); }
        return sb.ToString();
    }

    private static Dictionary<string, string> ListFiles(string folder)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            map[Path.GetRelativePath(folder, f)] = f;
        return map;
    }

    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string CompareMap(string name, string beforePath, string afterPath, int maxResults, int heightBlocks)
    {
        var sb = new StringBuilder();
        long blocksBefore = new FileInfo(beforePath).Length / BlockBytes;
        long blocksAfter = new FileInfo(afterPath).Length / BlockBytes;
        SourceScan.Add(sb, $"## Map detail for {name}");
        SourceScan.Add(sb, $"Blocks before: {blocksBefore}. Blocks after: {blocksAfter}. A block is an 8 by 8 square of tiles.");
        if (blocksBefore != blocksAfter) sb.AppendLine("The two maps are not the same size. Only the blocks they share are compared.");

        using var fa = new FileStream(beforePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        using var fb = new FileStream(afterPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        var bufA = new byte[BlockBytes];
        var bufB = new byte[BlockBytes];
        long block = 0, diffBlocks = 0, diffTiles = 0, diffZ = 0, listed = 0;
        var examples = new StringBuilder();
        while (ReadFull(fa, bufA) && ReadFull(fb, bufB))
        {
            bool blockDiffers = false;
            for (int cell = 0; cell < CellsPerBlock; cell++)
            {
                int off = HeaderBytes + cell * CellBytes;
                int tileA = bufA[off] | (bufA[off + 1] << 8);
                int tileB = bufB[off] | (bufB[off + 1] << 8);
                int zA = (sbyte)bufA[off + 2];
                int zB = (sbyte)bufB[off + 2];
                if (tileA == tileB && zA == zB) continue;
                blockDiffers = true;
                if (tileA != tileB) diffTiles++;
                if (zA != zB) diffZ++;
                if (listed < maxResults) { listed++; examples.AppendLine(DescribeCell(block, cell, heightBlocks, tileA, tileB, zA, zB)); }
            }
            if (blockDiffers) diffBlocks++;
            block++;
        }
        SourceScan.Add(sb, $"Blocks that differ: {diffBlocks}");
        SourceScan.Add(sb, $"Tiles with a different land tile ID: {diffTiles}");
        SourceScan.Add(sb, $"Tiles with a different height (Z): {diffZ}");
        if (examples.Length > 0)
        {
            sb.AppendLine(heightBlocks > 0 ? "First changes (X, Y):" : "First changes (block number and cell). Give facetHeightBlocks to get X and Y:");
            sb.Append(examples);
        }
        sb.AppendLine();
        return sb.ToString();
    }

    private static string DescribeCell(long block, int cell, int heightBlocks, int tileA, int tileB, int zA, int zB)
    {
        int cx = cell % BlockSide, cy = cell / BlockSide;
        string where = heightBlocks > 0
            ? $"({(block / heightBlocks * BlockSide) + cx}, {(block % heightBlocks * BlockSide) + cy})"
            : $"block {block}, cell {cell}";
        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"- {where}: tile {tileA} -> {tileB}, height {zA} -> {zB}");
    }

    private static bool ReadFull(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0) return false;
            total += read;
        }
        return true;
    }
}

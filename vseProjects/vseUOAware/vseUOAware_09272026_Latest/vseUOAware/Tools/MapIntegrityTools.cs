using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using vseUOAware.Services;

/// <summary>
/// Read-only whole-facet integrity check of the map, statics index and statics files.
/// </summary>
internal class MapIntegrityTools
{
    private const int MaxListed = 20;
    private const int IndexEntrySize = 12;
    private const int StaticRecordSize = 7;

    [McpServerTool]
    [Description("Checks a facet's map, staidx and statics files for structural problems: block count vs size, staidx entry count, index entries pointing past the end of the statics file, and record lengths not a multiple of 7. Read-only.")]
    public string CheckMapIntegrity(
        [Description("Facet number (0-5 defaults, higher = custom map).")] int? facet = null,
        [Description("Full path to the map file. Not needed when facet is given.")] string? mapUopFilePath = null,
        [Description("Full path to staticsN.mul. Not needed when facet is given.")] string? staticsMulFilePath = null,
        [Description("Full path to staidxN.mul. Not needed when facet is given.")] string? staIdxMulFilePath = null,
        [Description("Facet width in 8x8 blocks.")] int facetWidthBlocks = 0,
        [Description("Facet width in tiles (divisible by 8).")] int facetWidthTiles = 0,
        [Description("Facet height in tiles (divisible by 8).")] int facetHeightTiles = 0)
    {
        if (!MapAnalysisTools.Resolve(facet, mapUopFilePath, staticsMulFilePath, staIdxMulFilePath, facetWidthBlocks, facetWidthTiles, facetHeightTiles, true,
                out var map, out var sta, out var idx, out var wb, out var err))
            return err;
        try
        {
            var sb = new StringBuilder();
            long blocks = MapCoordinateReader.CountBlocks(map);
            int hb = MapCoordinateReader.ResolveHeightBlocks(map, wb);
            sb.AppendLine($"Map: {wb * 8} x {hb * 8} tiles, {wb} x {hb} blocks, file holds {blocks} blocks (expected {(long)wb * hb}).");

            long idxLen = new FileInfo(idx).Length;
            long staLen = new FileInfo(sta).Length;
            long entries = idxLen / IndexEntrySize;
            int problems = 0;
            if (idxLen % IndexEntrySize != 0) { problems++; sb.AppendLine($"PROBLEM: staidx size {idxLen} is not a multiple of {IndexEntrySize}."); }
            if (entries != blocks) { problems++; sb.AppendLine($"PROBLEM: staidx has {entries} entries but the map has {blocks} blocks."); }

            long withStatics = 0, totalRecords = 0, listed = 0, badEntries = 0;
            using (var fs = File.Open(idx, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(new BufferedStream(fs, 1 << 16)))
            {
                for (long i = 0; i < entries; i++)
                {
                    int offset = br.ReadInt32();
                    int length = br.ReadInt32();
                    br.ReadInt32();
                    if (offset < 0 || length <= 0) continue;
                    withStatics++;
                    string? why = null;
                    if (length % StaticRecordSize != 0) why = $"length {length} not a multiple of {StaticRecordSize}";
                    else if ((long)offset + length > staLen) why = $"offset {offset} + length {length} past end of statics ({staLen})";
                    if (why == null) { totalRecords += length / StaticRecordSize; continue; }
                    badEntries++;
                    if (listed < MaxListed)
                    {
                        listed++;
                        sb.AppendLine($"PROBLEM: block {i} (blockX {i / hb}, blockY {i % hb}): {why}");
                    }
                }
            }
            problems += (int)Math.Min(badEntries, int.MaxValue);
            sb.AppendLine($"Statics: {withStatics} blocks have statics, {totalRecords} valid records, {badEntries} bad index entries.");
            if (badEntries > listed) sb.AppendLine($"  ... {badEntries - listed} more bad entries not listed");
            sb.AppendLine(problems == 0 ? "RESULT: no structural problems found." : $"RESULT: {problems} problem(s) found.");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Failed to check map integrity: {ex.Message}";
        }
    }
}

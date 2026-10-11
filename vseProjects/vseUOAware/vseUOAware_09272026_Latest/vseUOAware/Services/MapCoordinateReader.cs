namespace vseUOAware.Services;

/// <summary>One static (placed) item found at a map coordinate.</summary>
internal sealed record MapStaticTileInfo(int TileId, int X, int Y, int Z, int Hue);

/// <summary>Land + statics info for a single world coordinate on one facet.</summary>
internal sealed record MapCoordinateInfo(int X, int Y, int LandTileId, int LandZ, IReadOnlyList<MapStaticTileInfo> Statics);

/// <summary>
/// Looks up land tile/Z and static items at a specific world (x, y) coordinate
/// on a facet, by combining the facet's land block data (from either a newer
/// map*LegacyMUL.uop container or a legacy mapN.mul flat file - both are
/// auto-detected and use the identical per-block tile layout) with the
/// classic staticsN.mul/staidxN.mul pair (statics). This relies on the same
/// "entries/blocks are laid out in sequential block order" assumption used
/// by RenderMapAscii, so results are best-effort rather than guaranteed exact.
/// </summary>
internal static class MapCoordinateReader
{
    private const int BlockSize = 4 + 64 * 3; // 4-byte header + 64 tiles * (ushort ID + sbyte Z)
    private const int StaticRecordSize = 7;    // ushort TileID + byte X + byte Y + sbyte Z + ushort Hue
    private const uint UopMagic = 0x0050594D; // "MYP\0" - same constant used by uopFileReader

    /// <summary>
    /// Returns true if the file at <paramref name="mapFilePath"/> is a UOP
    /// container (starts with the "MYP\0" magic), false if it should be
    /// treated as a legacy flat mapN.mul file.
    /// </summary>
    private static bool IsUopContainer(string mapFilePath)
    {
        using var stream = File.OpenRead(mapFilePath);
        if (stream.Length < 4)
            return false;

        using var reader = new BinaryReader(stream);
        return reader.ReadUInt32() == UopMagic;
    }

    /// <summary>
    /// UO map files store 8x8 blocks column by column (all Y blocks for X=0, then X=1, ...),
    /// so index = blockX * heightBlocks + blockY. Verified against map0.mul seam continuity.
    /// </summary>
    public static int BlockIndex(string mapFilePath, int facetWidthBlocks, int blockX, int blockY)
        => blockX * ResolveHeightBlocks(mapFilePath, facetWidthBlocks) + blockY;

    /// <summary>
    /// Counts the 196-byte map blocks in a flat mapN.mul, or in a UOP container by summing
    /// the decompressed length of every entry. Throws if the data is not a whole number of blocks.
    /// </summary>
    public static long CountBlocks(string mapFilePath)
    {
        bool isUop = IsUopContainer(mapFilePath);
        long bytes = isUop
            ? uopFileReader.ReadEntries(mapFilePath).Sum(e => (long)e.DecompressedLength)
            : new FileInfo(mapFilePath).Length;

        if (bytes <= 0 || bytes % BlockSize != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(mapFilePath)} holds {bytes} bytes, which is not a whole number of {BlockSize}-byte map blocks.");
        }

        long blocks = bytes / BlockSize;

        // Measured on map0-5 LegacyMUL.uop: every UOP holds exactly one block more than the
        // flat mul. Treated as UOP container padding (user-confirmed), so it is not map data.
        return isUop ? blocks - 1 : blocks;
    }

    /// <summary>
    /// Height in blocks = total blocks / width. Never guesses: a width that does not divide
    /// the block count evenly is an error, not a rounded answer.
    /// </summary>
    public static int ResolveHeightBlocks(string mapFilePath, int facetWidthBlocks)
    {
        if (facetWidthBlocks <= 0)
        {
            throw new InvalidOperationException("Facet width in blocks must be greater than 0.");
        }

        long blocks = CountBlocks(mapFilePath);
        if (blocks % facetWidthBlocks != 0)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(mapFilePath)} holds {blocks} blocks, which does not divide evenly by width {facetWidthBlocks} blocks ({facetWidthBlocks * 8} tiles). The width is wrong.");
        }

        return (int)(blocks / facetWidthBlocks);
    }

    /// <summary>
    /// Throws a clear error when the requested tile rectangle falls outside the facet.
    /// Facet size in tiles is blocks * 8.
    /// </summary>
    private static void EnsureInBounds(string mapFilePath, int facetWidthBlocks, int x, int y, int width, int height)
    {
        int widthTiles = facetWidthBlocks * 8;
        int heightBlocks = ResolveHeightBlocks(mapFilePath, facetWidthBlocks);
        int heightTiles = heightBlocks * 8;
        if (x < 0 || y < 0 || x + width > widthTiles || y + height > heightTiles)
        {
            throw new InvalidOperationException(
                $"Out of range: this map is {facetWidthBlocks} x {heightBlocks} blocks = {widthTiles} x {heightTiles} tiles ({widthTiles} x {heightTiles} px in a paint program, 1 px = 1 tile) (valid X 0-{widthTiles - 1}, Y 0-{heightTiles - 1}); requested X {x}-{x + width - 1}, Y {y}-{y + height - 1}.");
        }
    }

    public static MapCoordinateInfo GetCoordinate(
        string mapUopFilePath,
        string staticsMulFilePath,
        string staIdxMulFilePath,
        int facetWidthBlocks,
        int x,
        int y,
        bool includeStatics = true)
    {
        EnsureInBounds(mapUopFilePath, facetWidthBlocks, x, y, 1, 1);
        int blockX = x / 8;
        int blockY = y / 8;
        int tileXInBlock = x % 8;
        int tileYInBlock = y % 8;
        int blockIndex = BlockIndex(mapUopFilePath, facetWidthBlocks, blockX, blockY);

        var (landTileId, landZ) = ReadLand(mapUopFilePath, blockIndex, tileXInBlock, tileYInBlock);

        var statics = includeStatics
            ? ReadStatics(staticsMulFilePath, staIdxMulFilePath, blockIndex, tileXInBlock, tileYInBlock)
            : [];

        return new MapCoordinateInfo(x, y, landTileId, landZ, statics);
    }

    /// <summary>
    /// Reads a bounded rectangular region of world coordinates, returning
    /// land/statics for every tile. Reuses the UOP entry list and open file
    /// handles across the whole region instead of re-reading them per tile,
    /// making this far cheaper than repeated single-coordinate calls.
    /// </summary>
    public static List<MapCoordinateInfo> GetRegion(
        string mapUopFilePath,
        string staticsMulFilePath,
        string staIdxMulFilePath,
        int facetWidthBlocks,
        int x,
        int y,
        int width,
        int height,
        bool includeStatics = true)
    {
        EnsureInBounds(mapUopFilePath, facetWidthBlocks, x, y, width, height);
        bool isUop = IsUopContainer(mapUopFilePath);
        var entries = isUop ? uopFileReader.ReadEntries(mapUopFilePath).OrderBy(e => e.Offset).ToList() : null;
        var landBlockCache = new Dictionary<int, byte[]>();
        int heightBlocks = ResolveHeightBlocks(mapUopFilePath, facetWidthBlocks);

        using var idxStream = includeStatics ? File.OpenRead(staIdxMulFilePath) : null;
        using var idxReader = includeStatics ? new BinaryReader(idxStream!) : null;
        using var staticsStream = includeStatics ? File.OpenRead(staticsMulFilePath) : null;
        using var staticsReader = includeStatics ? new BinaryReader(staticsStream!) : null;

        var results = new List<MapCoordinateInfo>(width * height);

        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int worldX = x + dx;
                int worldY = y + dy;

                int blockX = worldX / 8;
                int blockY = worldY / 8;
                int tileXInBlock = worldX % 8;
                int tileYInBlock = worldY % 8;
                int blockIndex = blockX * heightBlocks + blockY;

                var (landTileId, landZ) = ReadLandCached(entries, mapUopFilePath, landBlockCache, blockIndex, tileXInBlock, tileYInBlock);

                var statics = includeStatics
                    ? ReadStaticsWithReaders(idxReader!, staticsReader!, staticsStream!, blockIndex, tileXInBlock, tileYInBlock)
                    : [];

                results.Add(new MapCoordinateInfo(worldX, worldY, landTileId, landZ, statics));
            }
        }

        return results;
    }

    private static (int TileId, int Z) ReadLandCached(
        List<UopEntry>? entries,
        string mapFilePath,
        Dictionary<int, byte[]> blockCache,
        int blockIndex,
        int tileXInBlock,
        int tileYInBlock)
    {
        if (!blockCache.TryGetValue(blockIndex, out var data) || data is null)
        {
            byte[]? found;

            if (entries is null)
            {
                // Legacy flat mapN.mul: blocks are stored back-to-back with
                // no container/index, so the block offset is direct.
                long blockOffset = (long)blockIndex * BlockSize;
                using var stream = File.OpenRead(mapFilePath);
                if (blockOffset + BlockSize > stream.Length)
                    throw new ArgumentOutOfRangeException(nameof(blockIndex), $"Block index {blockIndex} was not found in {mapFilePath}.");

                stream.Seek(blockOffset, SeekOrigin.Begin);
                found = new byte[BlockSize];
                stream.ReadExactly(found, 0, BlockSize);
            }
            else
            {
                long blocksSoFar = 0;
                found = null;

                foreach (var entry in entries)
                {
                    long blocksInEntry = entry.DecompressedLength / BlockSize;
                    if (blockIndex < blocksSoFar + blocksInEntry)
                    {
                        var entryData = uopFileReader.ReadEntryData(mapFilePath, entry);
                        int localBlockIndex = (int)(blockIndex - blocksSoFar);
                        int blockOffset2 = localBlockIndex * BlockSize;
                        found = entryData.AsSpan(blockOffset2, BlockSize).ToArray();
                        break;
                    }

                    blocksSoFar += blocksInEntry;
                }

                if (found is null)
                    throw new ArgumentOutOfRangeException(nameof(blockIndex), $"Block index {blockIndex} was not found in {mapFilePath}.");
            }

            data = found;
            blockCache[blockIndex] = data;
        }

        int tileIndex = tileYInBlock * 8 + tileXInBlock;
        int tileOffset = 4 + tileIndex * 3;

        int tileId = BitConverter.ToUInt16(data, tileOffset);
        int z = (sbyte)data[tileOffset + 2];
        return (tileId, z);
    }

    private static List<MapStaticTileInfo> ReadStaticsWithReaders(
        BinaryReader idxReader,
        BinaryReader staticsReader,
        FileStream staticsStream,
        int blockIndex,
        int tileXInBlock,
        int tileYInBlock)
    {
        long idxOffset = (long)blockIndex * 12;
        if (idxOffset + 12 > idxReader.BaseStream.Length)
            return [];

        idxReader.BaseStream.Seek(idxOffset, SeekOrigin.Begin);
        int offset = idxReader.ReadInt32();
        int length = idxReader.ReadInt32();
        idxReader.ReadInt32(); // extra - unused here

        if (offset < 0 || length <= 0)
            return [];

        staticsStream.Seek(offset, SeekOrigin.Begin);

        int recordCount = length / StaticRecordSize;
        var results = new List<MapStaticTileInfo>();

        for (int i = 0; i < recordCount; i++)
        {
            int tileId = staticsReader.ReadUInt16();
            int recordX = staticsReader.ReadByte();
            int recordY = staticsReader.ReadByte();
            int z = staticsReader.ReadSByte();
            int hue = staticsReader.ReadUInt16();

            if (recordX == tileXInBlock && recordY == tileYInBlock)
                results.Add(new MapStaticTileInfo(tileId, recordX, recordY, z, hue));
        }

        return results;
    }

    private static (int TileId, int Z) ReadLand(string mapFilePath, int blockIndex, int tileXInBlock, int tileYInBlock)
    {
        int tileIndex = tileYInBlock * 8 + tileXInBlock;

        if (!IsUopContainer(mapFilePath))
        {
            // Legacy flat mapN.mul: blocks are stored back-to-back with no
            // container/index, so the block offset is direct.
            long blockOffset = (long)blockIndex * BlockSize;
            using var stream = File.OpenRead(mapFilePath);
            if (blockOffset + BlockSize > stream.Length)
                throw new ArgumentOutOfRangeException(nameof(blockIndex), $"Block index {blockIndex} was not found in {mapFilePath}.");

            stream.Seek(blockOffset + 4 + tileIndex * 3, SeekOrigin.Begin);
            using var reader = new BinaryReader(stream);
            int tileId0 = reader.ReadUInt16();
            int z0 = reader.ReadSByte();
            return (tileId0, z0);
        }

        var entries = uopFileReader.ReadEntries(mapFilePath).OrderBy(e => e.Offset).ToList();

        long blocksSoFar = 0;
        foreach (var entry in entries)
        {
            long blocksInEntry = entry.DecompressedLength / BlockSize;
            if (blockIndex < blocksSoFar + blocksInEntry)
            {
                var data = uopFileReader.ReadEntryData(mapFilePath, entry);
                int localBlockIndex = (int)(blockIndex - blocksSoFar);
                int blockOffset = localBlockIndex * BlockSize;
                int tileOffset = blockOffset + 4 + tileIndex * 3;

                int tileId = BitConverter.ToUInt16(data, tileOffset);
                int z = (sbyte)data[tileOffset + 2];
                return (tileId, z);
            }

            blocksSoFar += blocksInEntry;
        }

        throw new ArgumentOutOfRangeException(nameof(blockIndex), $"Block index {blockIndex} was not found in {mapFilePath}.");
    }

    private static List<MapStaticTileInfo> ReadStatics(
        string staticsMulFilePath,
        string staIdxMulFilePath,
        int blockIndex,
        int tileXInBlock,
        int tileYInBlock)
    {
        using var idxStream = File.OpenRead(staIdxMulFilePath);
        long idxOffset = (long)blockIndex * 12;
        if (idxOffset + 12 > idxStream.Length)
            return [];

        using var idxReader = new BinaryReader(idxStream);
        idxStream.Seek(idxOffset, SeekOrigin.Begin);
        int offset = idxReader.ReadInt32();
        int length = idxReader.ReadInt32();
        idxReader.ReadInt32(); // extra - unused here

        if (offset < 0 || length <= 0)
            return [];

        using var staticsStream = File.OpenRead(staticsMulFilePath);
        staticsStream.Seek(offset, SeekOrigin.Begin);
        using var staticsReader = new BinaryReader(staticsStream);

        int recordCount = length / StaticRecordSize;
        var results = new List<MapStaticTileInfo>();

        for (int i = 0; i < recordCount; i++)
        {
            int tileId = staticsReader.ReadUInt16();
            int recordX = staticsReader.ReadByte();
            int recordY = staticsReader.ReadByte();
            int z = staticsReader.ReadSByte();
            int hue = staticsReader.ReadUInt16();

            if (recordX == tileXInBlock && recordY == tileYInBlock)
                results.Add(new MapStaticTileInfo(tileId, recordX, recordY, z, hue));
        }

        return results;
    }
}

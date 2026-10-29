namespace Heroes.MpqTool;

internal class MpqHeader
{
    private const uint MpqId = 0x1a51504d; // "MPQ\x1A"
    private const uint UserDataId = 0x1b51504d; // "MPQ\x1B"
    private const int Size = 32; // base header (format version 0)
    private const int ExtendedSize = 44; // format version 1 and later add 12 bytes of high offset bits

    private MpqHeader(ref BitReader bitReaderStruct, long headerOffset)
    {
        HeaderOffset = headerOffset;

        bitReaderStruct.Index = 4; // skip the id, the caller already checked it

        DataOffset = bitReaderStruct.ReadUInt32Aligned();
        ArchiveSize = bitReaderStruct.ReadUInt32Aligned();
        MpqVersion = bitReaderStruct.ReadUInt16Aligned();
        BlockSize = bitReaderStruct.ReadUInt16Aligned();
        HashTablePos = bitReaderStruct.ReadUInt32Aligned();
        BlockTablePos = bitReaderStruct.ReadUInt32Aligned();
        HashTableSize = bitReaderStruct.ReadUInt32Aligned();
        BlockTableSize = bitReaderStruct.ReadUInt32Aligned();

        if (MpqVersion >= 1)
        {
            ExtendedBlockTableOffset = bitReaderStruct.ReadInt64Aligned();
            HashTableOffsetHigh = bitReaderStruct.ReadInt16Aligned();
            BlockTableOffsetHigh = bitReaderStruct.ReadInt16Aligned();
        }

        HashTablePos += (uint)HeaderOffset;
        BlockTablePos += (uint)HeaderOffset;

        if (DataOffset == 0x6d9e4b86) // A protected archive.
            DataOffset = (uint)(Size + HeaderOffset);
    }

    public uint DataOffset { get; private set; } // Offset of the first file.  AKA Header size

    public uint ArchiveSize { get; private set; }

    public ushort MpqVersion { get; private set; }

    public ushort BlockSize { get; private set; } // Size of file block is 0x200 << BlockSize

    public uint HashTablePos { get; private set; }

    public uint BlockTablePos { get; private set; }

    public uint HashTableSize { get; private set; }

    public uint BlockTableSize { get; private set; }

    // Version 1 fields
    // The extended block table is an array of Int16 - higher bits of the offsets in the block table.
    public long ExtendedBlockTableOffset { get; private set; }

    public short HashTableOffsetHigh { get; private set; }

    public short BlockTableOffsetHigh { get; private set; }

    // Offset of the MPQ header from the start of the archive.
    public long HeaderOffset { get; private set; }

    /// <summary>
    /// Reads the MPQ header from <paramref name="stream"/>, which must be positioned at <paramref name="archiveStart"/>.
    /// </summary>
    /// <param name="stream">The archive stream.</param>
    /// <param name="archiveStart">The stream position where the archive starts.</param>
    /// <returns>The parsed header.</returns>
    /// <exception cref="MpqHeroesToolException">The MPQ header could not be found.</exception>
    public static MpqHeader Read(Stream stream, long archiveStart)
    {
        Span<byte> buffer = stackalloc byte[ExtendedSize];

        // The archive starts with either the MPQ header or a user data header (replays and maps).
        // The third field of the user data header is the offset of the MPQ header.
        stream.ReadExactly(buffer[..Size]);
        long headerOffset = 0;

        if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) == UserDataId)
        {
            headerOffset = BinaryPrimitives.ReadUInt32LittleEndian(buffer[8..]);
            stream.Position = archiveStart + headerOffset;
            stream.ReadExactly(buffer[..Size]);
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) != MpqId)
            throw new MpqHeroesToolException("Could not locate the header");

        int length = BinaryPrimitives.ReadUInt16LittleEndian(buffer[12..]) >= 1 ? ExtendedSize : Size;

        if (length > Size)
            stream.ReadExactly(buffer[Size..length]);

        BitReader bitReader = new(buffer[..length], EndianType.LittleEndian);

        return new MpqHeader(ref bitReader, headerOffset);
    }
}

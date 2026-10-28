using Heroes.MpqTool.BZip2;
using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Heroes.MpqTool;

/// <summary>
/// An mpq heroes file.
/// </summary>
public class MpqHeroesArchive : IDisposable
{
    /// <summary>
    /// Gets the size of the header.
    /// </summary>
    public const int HeaderSize = 0x100;

    private const int MaxStackAllocLimit = 1024;

    // The most compressed bytes read from the stream at once when decompressing a multi-block entry.
    private const int MaxBlockReadLength = 0x100000;

    private static readonly uint[] _stormBuffer = BuildStormBuffer();

    private readonly Stream _archiveStream;

    // where the archive starts in the stream, the positions in the archive are relative to it
    private readonly long _archiveStartPosition;
    private readonly MpqHeroesArchiveEntry[] _mpqArchiveEntries;
    private readonly int _blockSize;

    private MpqHeader _mpqHeader;
    private MpqHash[] _mpqHashes;
    private bool _isDisposed = false;

    internal MpqHeroesArchive(Stream stream)
    {
        _archiveStream = stream ?? throw new ArgumentNullException(nameof(stream));
        _archiveStartPosition = stream.Position;

        SetMpqHeader(stream);

        _blockSize = 0x200 << _mpqHeader.BlockSize;

        LoadHashTable(stream);
        _mpqArchiveEntries = LoadEntryTable(stream);

        AddListfileFileNames();
    }

    /// <summary>
    /// Gets an array of the archives entries.
    /// </summary>
    public MpqHeroesArchiveEntry[] MpqArchiveEntries => _mpqArchiveEntries;

    /// <summary>
    /// Gets the headers in bytes.
    /// </summary>
    /// <param name="size">The number of bytes of the header.</param>
    /// <returns>A read-only span of bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> cannot be less than 1.</exception>
    public Stream GetHeaderBytes(int size = HeaderSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);

        byte[] data = new byte[size];
        _archiveStream.Position = _archiveStartPosition;
        _archiveStream.ReadExactly(data);

        return new MemoryStream(data, writable: false);
    }

    /// <summary>
    /// Gets the headers in bytes.
    /// </summary>
    /// <param name="buffer">A span to read in the bytes.</param>
    public void GetHeaderBytes(Span<byte> buffer)
    {
        _archiveStream.Position = _archiveStartPosition;
        _archiveStream.ReadExactly(buffer);
    }

    /// <summary>
    /// Gets a <see cref="MpqHeroesArchiveEntry"/> by its file name.
    /// </summary>
    /// <param name="fileName">The name of the archive entry. Is case-insensitive.</param>
    /// <param name="normalizeSlashes">If <see langword="true"/>, replaces <c>/</c> with <c>\</c> before lookup.</param>
    /// <returns>An <see cref="MpqHeroesArchiveEntry"/>.</returns>
    /// <exception cref="FileNotFoundException">The <paramref name="fileName"/> was not found.</exception>
    public MpqHeroesArchiveEntry GetEntry(string fileName, bool normalizeSlashes = false)
    {
        return GetEntry(fileName.AsSpan(), normalizeSlashes);
    }

    /// <summary>
    /// Gets a <see cref="MpqHeroesArchiveEntry"/> by its file name.
    /// </summary>
    /// <param name="fileName">The name of the archive entry. Is case-insensitive.</param>
    /// <param name="normalizeSlashes">If <see langword="true"/>, replaces <c>/</c> with <c>\</c> before lookup.</param>
    /// <returns>An <see cref="MpqHeroesArchiveEntry"/>.</returns>
    /// <exception cref="FileNotFoundException">The <paramref name="fileName"/> was not found.</exception>
    public MpqHeroesArchiveEntry GetEntry(ReadOnlySpan<char> fileName, bool normalizeSlashes = false)
    {
        if (!TryGetEntry(fileName, out MpqHeroesArchiveEntry? entry, normalizeSlashes))
            throw new FileNotFoundException("File not found", fileName.ToString());

        return entry.Value;
    }

    /// <summary>
    /// Tries to get a <see cref="MpqHeroesArchiveEntry"/> by it's file name.
    /// </summary>
    /// <param name="fileName">The name of the archive entry. Is case-insensitive.</param>
    /// <param name="mpqHeroesArchiveEntry">When this method returns, contains the <see cref="MpqHeroesArchiveEntry"/>.</param>
    /// <param name="normalizeSlashes">If <see langword="true"/>, replaces <c>/</c> with <c>\</c> before lookup.</param>
    /// <returns><see langword="true"/> if the value was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetEntry(string fileName, [NotNullWhen(true)] out MpqHeroesArchiveEntry? mpqHeroesArchiveEntry, bool normalizeSlashes = false)
    {
        return TryGetEntry(fileName.AsSpan(), out mpqHeroesArchiveEntry, normalizeSlashes);
    }

    /// <summary>
    /// Tries to get a <see cref="MpqHeroesArchiveEntry"/> by it's file name.
    /// </summary>
    /// <param name="fileName">The name of the archive entry. Is case-insensitive.</param>
    /// <param name="mpqHeroesArchiveEntry">When this method returns, contains the <see cref="MpqHeroesArchiveEntry"/>.</param>
    /// <param name="normalizeSlashes">If <see langword="true"/>, replaces <c>/</c> with <c>\</c> before lookup.</param>
    /// <returns><see langword="true"/> if the value was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetEntry(ReadOnlySpan<char> fileName, [NotNullWhen(true)] out MpqHeroesArchiveEntry? mpqHeroesArchiveEntry, bool normalizeSlashes = false)
    {
        mpqHeroesArchiveEntry = null;

        if (!TryGetHashEntry(fileName, out MpqHash hash, normalizeSlashes))
            return false;

        MpqHeroesArchiveEntry entry = _mpqArchiveEntries[hash.BlockIndex];

        mpqHeroesArchiveEntry = entry;

        return true;
    }

    /// <summary>
    /// Checks if the entry exist.
    /// </summary>
    /// <param name="fileName">The name of the archive entry. Is case-insensitive.</param>
    /// <param name="normalizeSlashes">If <see langword="true"/>, replaces <c>/</c> with <c>\</c> before lookup.</param>
    /// <returns><see langword="true"/> if the entry exists, otherwise returns <see langword="false"/>.</returns>
    public bool FileEntryExists(string fileName, bool normalizeSlashes = false) => FileEntryExists(fileName.AsSpan(), normalizeSlashes);

    /// <summary>
    /// Checks if the entry exist.
    /// </summary>
    /// <param name="fileName">The name of the archive entry. Is case-insensitive.</param>
    /// <param name="normalizeSlashes">If <see langword="true"/>, replaces <c>/</c> with <c>\</c> before lookup.</param>
    /// <returns><see langword="true"/> if the entry exists, otherwise returns <see langword="false"/>.</returns>
    public bool FileEntryExists(ReadOnlySpan<char> fileName, bool normalizeSlashes = false) => TryGetHashEntry(fileName, out _, normalizeSlashes);

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Decompresses an <see cref="MpqHeroesArchiveEntry"/> and writes its data into a buffer.
    /// </summary>
    /// <param name="mpqArchiveEntry">The <see cref="MpqHeroesArchiveEntry"/> to decompress.</param>
    /// <param name="buffer">The buffer of the decompressed <see cref="MpqHeroesArchiveEntry"/>.</param>
    /// <exception cref="MpqHeroesToolException">An exception occured.</exception>
    public void DecompressEntry(MpqHeroesArchiveEntry mpqArchiveEntry, Span<byte> buffer)
    {
        if (mpqArchiveEntry.IsCompressed && !mpqArchiveEntry.IsSingleUnit)
        {
            if (mpqArchiveEntry.IsEncrypted)
                throw new MpqHeroesToolException("The entry is encrypted.");

            DecompressBlocks(mpqArchiveEntry, buffer);
        }
        else
        {
            DecompressSingleUnit(mpqArchiveEntry, buffer);
        }
    }

    /// <summary>
    /// Decompresses an <see cref="MpqHeroesArchiveEntry"/> and returns it as a stream.
    /// </summary>
    /// <param name="mpqArchiveEntry">The <see cref="MpqHeroesArchiveEntry"/> to decompress.</param>
    /// <returns>A read-only stream of the contents of the entry.</returns>
    public Stream DecompressEntry(MpqHeroesArchiveEntry mpqArchiveEntry)
    {
        // DecompressEntry writes every byte of the buffer (or throws), so it doesn't need zeroing first.
        byte[] buffer = GC.AllocateUninitializedArray<byte>((int)mpqArchiveEntry.FileSize);

        DecompressEntry(mpqArchiveEntry, buffer);

        // publiclyVisible lets callers get the underlying buffer with TryGetBuffer instead of copying it.
        return new MemoryStream(buffer, 0, buffer.Length, writable: false, publiclyVisible: true);
    }

    internal static uint HashString(ReadOnlySpan<char> input, int offset, bool normalizeSlashes = false)
    {
        uint seed1 = 0x7fed7fed;
        uint seed2 = 0xeeeeeeee;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (normalizeSlashes && c == '/')
                c = '\\';

            c = char.ToUpperInvariant(c);
            seed1 = _stormBuffer[offset + c] ^ (seed1 + seed2);
            seed2 = c + seed1 + seed2 + (seed2 << 5) + 3;
        }

        return seed1;
    }

    internal static void DecryptBlock(Span<uint> data, uint seed1)
    {
        uint seed2 = 0xeeeeeeee;

        for (int i = 0; i < data.Length; i++)
        {
            seed2 += _stormBuffer[0x400 + (seed1 & 0xff)];
            uint result = data[i];
            result ^= seed1 + seed2;

            seed1 = ((~seed1 << 21) + 0x11111111) | (seed1 >> 11);
            seed2 = result + seed2 + (seed2 << 5) + 3;
            data[i] = result;
        }
    }

    // This function calculates the encryption key based on
    // some assumptions we can make about the headers for encrypted files
    internal static uint DetectFileSeed(uint value0, uint value1, uint decrypted)
    {
        uint temp = (value0 ^ decrypted) - 0xeeeeeeee;

        for (int i = 0; i < 0x100; i++)
        {
            uint seed1 = temp - _stormBuffer[0x400 + i];
            uint seed2 = 0xeeeeeeee + _stormBuffer[0x400 + (seed1 & 0xff)];
            uint result = value0 ^ (seed1 + seed2);

            if (result != decrypted)
                continue;

            uint saveseed1 = seed1;

            // Test this result against the 2nd value
            seed1 = ((~seed1 << 21) + 0x11111111) | (seed1 >> 11);
            seed2 = result + seed2 + (seed2 << 5) + 3;

            seed2 += _stormBuffer[0x400 + (seed1 & 0xff)];
            result = value1 ^ (seed1 + seed2);

            if ((result & 0xfffc0000) == 0)
                return saveseed1;
        }

        return 0;
    }

    /// <summary>
    /// Release the unmanaged and managed resources.
    /// </summary>
    /// <param name="disposing">Value indicating if disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed && disposing)
        {
            if (disposing)
            {
                _archiveStream?.Dispose();
            }

            _isDisposed = true;
        }
    }

    private static uint[] BuildStormBuffer()
    {
        uint seed = 0x100001;

        uint[] result = new uint[0x500];

        for (uint index1 = 0; index1 < 0x100; index1++)
        {
            uint index2 = index1;
            for (int i = 0; i < 5; i++, index2 += 0x100)
            {
                seed = ((seed * 125) + 3) % 0x2aaaab;
                uint temp = (seed & 0xffff) << 16;
                seed = ((seed * 125) + 3) % 0x2aaaab;

                result[index2] = temp | (seed & 0xffff);
            }
        }

        return result;
    }

    // Computes the hash table index and the two name hashes of a file name in a single pass.
    // Gives the same results as HashString with offsets 0, 0x100 and 0x200.
    private static (uint Index, uint Name1, uint Name2) HashFileName(ReadOnlySpan<char> fileName, bool normalizeSlashes)
    {
        ReadOnlySpan<uint> stormBuffer = _stormBuffer;

        uint indexSeed1 = 0x7fed7fed;
        uint indexSeed2 = 0xeeeeeeee;
        uint name1Seed1 = 0x7fed7fed;
        uint name1Seed2 = 0xeeeeeeee;
        uint name2Seed1 = 0x7fed7fed;
        uint name2Seed2 = 0xeeeeeeee;

        foreach (char ch in fileName)
        {
            char c = normalizeSlashes && ch == '/' ? '\\' : ch;
            c = char.ToUpperInvariant(c);

            indexSeed1 = stormBuffer[c] ^ (indexSeed1 + indexSeed2);
            indexSeed2 = c + indexSeed1 + indexSeed2 + (indexSeed2 << 5) + 3;

            name1Seed1 = stormBuffer[0x100 + c] ^ (name1Seed1 + name1Seed2);
            name1Seed2 = c + name1Seed1 + name1Seed2 + (name1Seed2 << 5) + 3;

            name2Seed1 = stormBuffer[0x200 + c] ^ (name2Seed1 + name2Seed2);
            name2Seed2 = c + name2Seed1 + name2Seed2 + (name2Seed2 << 5) + 3;
        }

        return (indexSeed1, name1Seed1, name2Seed1);
    }

    // Reads an encrypted table (the hash table or block table) and decrypts it in place.
    private static void ReadTable(Stream stream, long position, Span<uint> table, string key)
    {
        stream.Position = position;
        stream.ReadExactly(MemoryMarshal.AsBytes(table));

        if (!BitConverter.IsLittleEndian)
            BinaryPrimitives.ReverseEndianness(table, table);

        DecryptBlock(table, HashString(key, 0x300));
    }

    // source[offset] is the compression type and the compressed data follows it.
    private static void DecompressByteData(byte[] source, int offset, int length, Span<byte> destination)
    {
        if (length < 1)
            throw new MpqHeroesToolException("Insufficient data or invalid data length");

        byte compressionType = source[offset];

        switch (compressionType)
        {
            case 1: // Huffman
                throw new MpqHeroesToolException("Huffman not yet supported");
            case 2: // ZLib/Deflate
                ZlibDecompress(source, offset + 1, length - 1, destination);
                break;
            case 8: // PKLib/Impode
                throw new MpqHeroesToolException("PKLib/Impode not yet supported");
            case 0x10:
                BZip2Decoder.Decompress(source.AsSpan(offset + 1, length - 1), destination);
                break;
            case 0x80: // IMA ADPCM Stereo
                throw new MpqHeroesToolException("IMA ADPCM Stereo not yet supported");
            case 0x40: // IMA ADPCM Mono
                throw new MpqHeroesToolException("IMA ADPCM Mono not yet supported");
            case 0x12:
                throw new MpqHeroesToolException("LZMA compression not yet supported");

            // Combos
            case 0x22:
                throw new MpqHeroesToolException("Sparse compression + Deflate compression not yet supported");
            case 0x30:
                throw new MpqHeroesToolException("Sparse compression + BZip2 compression not yet supported");
            case 0x41:
                throw new MpqHeroesToolException("Not yet supported");
            case 0x48:
                throw new MpqHeroesToolException("Not yet supported");
            case 0x81:
                throw new MpqHeroesToolException("Not yet supported");
            case 0x88:
                throw new MpqHeroesToolException("Not yet supported");
            default:
                throw new MpqHeroesToolException("Compression is not yet supported: 0x" + compressionType.ToString("X"));
        }
    }

    private static void ZlibDecompress(byte[] source, int offset, int count, Span<byte> destination)
    {
        using MemoryStream memoryStream = new(source, offset, count, writable: false);
        using ZLibStream stream = new(memoryStream, CompressionMode.Decompress, false);

        stream.ReadExactly(destination);
    }

    [MemberNotNull(nameof(_mpqHeader))]
    private void SetMpqHeader(Stream stream)
    {
        Span<byte> headerBuffer = stackalloc byte[2048]; // guess how much the header will be
        stream.ReadExactly(headerBuffer);

        BitReader bitReader = new(headerBuffer, EndianType.LittleEndian);

        _mpqHeader = new MpqHeader(ref bitReader);

        if (_mpqHeader.HashTableOffsetHigh != 0 || _mpqHeader.ExtendedBlockTableOffset != 0 || _mpqHeader.BlockTableOffsetHigh != 0)
            throw new MpqHeroesToolException("MPQ format version 1 features are not supported");
    }

    [MemberNotNull(nameof(_mpqHashes))]
    private void LoadHashTable(Stream stream)
    {
        _mpqHashes = new MpqHash[_mpqHeader.HashTableSize];

        ReadTable(stream, _archiveStartPosition + _mpqHeader.HashTablePos, MemoryMarshal.Cast<MpqHash, uint>(_mpqHashes.AsSpan()), "(hash table)");
    }

    private MpqHeroesArchiveEntry[] LoadEntryTable(Stream stream)
    {
        int entryCount = (int)_mpqHeader.BlockTableSize;
        int tableLength = entryCount * 4; // four uints per entry

        uint[]? rentedTable = null;
        Span<uint> table = tableLength <= MaxStackAllocLimit / sizeof(uint)
            ? stackalloc uint[MaxStackAllocLimit / sizeof(uint)]
            : (rentedTable = ArrayPool<uint>.Shared.Rent(tableLength));

        table = table[..tableLength];

        try
        {
            ReadTable(stream, _archiveStartPosition + _mpqHeader.BlockTablePos, table, "(block table)");

            MpqHeroesArchiveEntry[] entries = new MpqHeroesArchiveEntry[entryCount];
            uint headerOffset = (uint)_mpqHeader.HeaderOffset;

            for (int i = 0; i < entries.Length; i++)
                entries[i] = new MpqHeroesArchiveEntry(table.Slice(i * 4, 4), headerOffset);

            return entries;
        }
        finally
        {
            if (rentedTable is not null)
                ArrayPool<uint>.Shared.Return(rentedTable);
        }
    }

    private void AddListfileFileNames()
    {
        const string listfileName = "(listfile)";

        if (!TryGetHashEntry(listfileName, out MpqHash hash))
            return;

        ref MpqHeroesArchiveEntry listfileEntry = ref _mpqArchiveEntries[hash.BlockIndex];
        listfileEntry.FileName = listfileName;

        int size = (int)listfileEntry.FileSize;

        byte[]? rentedBuffer = null;
        Span<byte> buffer = size <= MaxStackAllocLimit
            ? stackalloc byte[MaxStackAllocLimit]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(size));
        buffer = buffer[..size];

        try
        {
            DecompressEntry(listfileEntry, buffer);
            AddFileNames(buffer);
        }
        finally
        {
            if (rentedBuffer is not null)
                ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    // Each line of the listfile is a file name. Lines end with \n (Unix), \r\n (DOS) or \r (Mac).
    private void AddFileNames(ReadOnlySpan<byte> source)
    {
        while (!source.IsEmpty)
        {
            int lineLength = source.IndexOfAny((byte)'\r', (byte)'\n');

            if (lineLength < 0)
            {
                AddFileName(Encoding.UTF8.GetString(source));
                break;
            }

            if (lineLength > 0)
                AddFileName(Encoding.UTF8.GetString(source[..lineLength]));

            int nextLine = lineLength + 1;

            if (source[lineLength] == '\r' && nextLine < source.Length && source[nextLine] == '\n')
                nextLine++;

            source = source[nextLine..];
        }
    }

    private void AddFileName(string fileName)
    {
        if (TryGetHashEntry(fileName, out MpqHash hash))
            _mpqArchiveEntries[hash.BlockIndex].FileName = fileName;
    }

    private void DecompressSingleUnit(MpqHeroesArchiveEntry mpqArchiveEntry, Span<byte> buffer)
    {
        int compressedSize = (int)mpqArchiveEntry.CompressedSize;

        // stored as is
        if (mpqArchiveEntry.CompressedSize == mpqArchiveEntry.FileSize)
        {
            ReadSingleUnit(mpqArchiveEntry, buffer[..compressedSize]);
            return;
        }

        // The compressed data is read into its own buffer so it can be decompressed straight into the destination.
        byte[] compressed = ArrayPool<byte>.Shared.Rent(compressedSize);

        try
        {
            ReadSingleUnit(mpqArchiveEntry, compressed.AsSpan(0, compressedSize));
            DecompressByteData(compressed, 0, compressedSize, buffer[..Math.Min(buffer.Length, (int)mpqArchiveEntry.FileSize)]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compressed);
        }
    }

    private void ReadSingleUnit(MpqHeroesArchiveEntry mpqArchiveEntry, Span<byte> buffer)
    {
        _archiveStream.Position = _archiveStartPosition + mpqArchiveEntry.FilePosition;

        if (_archiveStream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false) != buffer.Length)
            throw new MpqHeroesToolException("Insufficient data or invalid data length");
    }

    private void DecompressBlocks(MpqHeroesArchiveEntry mpqArchiveEntry, Span<byte> buffer)
    {
        int fileSize = (int)mpqArchiveEntry.FileSize;
        int blockCount = (int)((mpqArchiveEntry.FileSize + (long)_blockSize - 1) / _blockSize);

        if (blockCount == 0)
            return;

        // The entry starts with a table of block offsets (relative to the entry): the start of each block, then the end of the last one.
        // Files with metadata have an extra offset after those for a block of checksums, which isn't needed here.
        int positionCount = blockCount + 1;

        uint[]? rentedPositions = null;
        Span<uint> blockPositions = positionCount <= MaxStackAllocLimit / sizeof(uint)
            ? stackalloc uint[MaxStackAllocLimit / sizeof(uint)]
            : (rentedPositions = ArrayPool<uint>.Shared.Rent(positionCount));

        blockPositions = blockPositions[..positionCount];

        byte[]? compressed = null;

        try
        {
            _archiveStream.Position = _archiveStartPosition + mpqArchiveEntry.FilePosition;
            _archiveStream.ReadExactly(MemoryMarshal.AsBytes(blockPositions));

            if (!BitConverter.IsLittleEndian)
                BinaryPrimitives.ReverseEndianness(blockPositions, blockPositions);

            uint largestBlock = 0;

            for (int i = 0; i < blockCount; i++)
            {
                if (blockPositions[i + 1] < blockPositions[i])
                    throw new MpqHeroesToolException("Invalid block position table");

                largestBlock = Math.Max(largestBlock, blockPositions[i + 1] - blockPositions[i]);
            }

            // The blocks are stored back to back, so read as many as fit in the buffer with each read instead of one at a time.
            uint totalLength = blockPositions[blockCount] - blockPositions[0];
            compressed = ArrayPool<byte>.Shared.Rent((int)Math.Min(totalLength, Math.Max(MaxBlockReadLength, largestBlock)));

            int block = 0;

            while (block < blockCount)
            {
                uint readStart = blockPositions[block];
                int readEndBlock = block + 1;

                while (readEndBlock < blockCount && blockPositions[readEndBlock + 1] - readStart <= compressed.Length)
                    readEndBlock++;

                _archiveStream.Position = _archiveStartPosition + mpqArchiveEntry.FilePosition + readStart;
                _archiveStream.ReadExactly(compressed.AsSpan(0, (int)(blockPositions[readEndBlock] - readStart)));

                for (; block < readEndBlock; block++)
                {
                    int blockOffset = block * _blockSize;
                    int expectedLength = Math.Min(fileSize - blockOffset, _blockSize);
                    int compressedOffset = (int)(blockPositions[block] - readStart);
                    int compressedLength = (int)(blockPositions[block + 1] - blockPositions[block]);
                    Span<byte> destination = buffer.Slice(blockOffset, expectedLength);

                    // a block that didn't get smaller when compressed is stored as is
                    if (compressedLength == expectedLength)
                        compressed.AsSpan(compressedOffset, compressedLength).CopyTo(destination);
                    else if ((mpqArchiveEntry.Flags & MpqFileFlags.CompressedMulti) != 0)
                        DecompressByteData(compressed, compressedOffset, compressedLength, destination);
                    else
                        throw new MpqHeroesToolException("Non-single unit must be decompresssed using pk");
                }
            }
        }
        finally
        {
            if (rentedPositions is not null)
                ArrayPool<uint>.Shared.Return(rentedPositions);

            if (compressed is not null)
                ArrayPool<byte>.Shared.Return(compressed);
        }
    }

    private bool TryGetHashEntry(ReadOnlySpan<char> fileName, out MpqHash hash, bool normalizeSlashes = false)
    {
        MpqHash[] hashes = _mpqHashes;

        if (hashes.Length > 0)
        {
            (uint index, uint name1, uint name2) = HashFileName(fileName, normalizeSlashes);

            int i = (int)(index & (uint)(hashes.Length - 1));

            // Probe forward from the file's slot, wrapping around. Entries are never stored past an empty slot, so stop there.
            for (int probed = 0; probed < hashes.Length; probed++)
            {
                ref readonly MpqHash candidate = ref hashes[i];

                if (candidate.Name1 == name1 && candidate.Name2 == name2)
                {
                    hash = candidate;
                    return true;
                }

                if (candidate.BlockIndex == MpqHash.EmptyBlockIndex)
                    break;

                if (++i == hashes.Length)
                    i = 0;
            }
        }

        hash = default;
        return false;
    }
}

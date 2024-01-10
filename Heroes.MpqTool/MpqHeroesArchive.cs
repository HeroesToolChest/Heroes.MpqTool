using Ionic.BZip2;
using Ionic.Zlib;

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

    private const int MaxStackAllocLimit = 2048;

    private static readonly uint[] _stormBuffer = BuildStormBuffer();

    private readonly Stream _archiveStream;
    private readonly MpqHeader _mpqHeader;

    private readonly MpqHash[] _mpqHashes;
    private readonly MpqHeroesArchiveEntry[] _mpqArchiveEntries;

    private readonly int _blockSize;
    private bool _isDisposed = false;

    internal MpqHeroesArchive(Stream stream)
    {
        _archiveStream = stream ?? throw new ArgumentNullException(nameof(stream));

        Span<byte> headerBuffer = stackalloc byte[MaxStackAllocLimit]; // guess how much the header will be
        stream.Read(headerBuffer);

        BitReader bitReader = new(headerBuffer, EndianType.LittleEndian);

        _mpqHeader = new MpqHeader(ref bitReader);

        if (_mpqHeader.HashTableOffsetHigh != 0 || _mpqHeader.ExtendedBlockTableOffset != 0 || _mpqHeader.BlockTableOffsetHigh != 0)
            throw new MpqHeroesToolException("MPQ format version 1 features are not supported");

        _blockSize = 0x200 << _mpqHeader.BlockSize;

        // LoadHashTable
        int hashBufferLength = (int)(_mpqHeader.HashTableSize * MpqHash.Size);
        Span<byte> hashBuffer = hashBufferLength <= MaxStackAllocLimit ? stackalloc byte[hashBufferLength] : new byte[hashBufferLength]; // get the hash table buffer

        stream.Position = (int)_mpqHeader.HashTablePos;
        stream.Read(hashBuffer);

        DecryptTable(hashBuffer, "(hash table)");

        _mpqHashes = new MpqHash[_mpqHeader.HashTableSize];

        BitReader mpqHashesBitReader = new(hashBuffer, EndianType.LittleEndian);

        for (int i = 0; i < _mpqHeader.HashTableSize; i++)
            _mpqHashes[i] = new MpqHash(ref mpqHashesBitReader);

        // LoadEntryTable;
        int entryBufferLength = (int)(_mpqHeader.BlockTableSize * MpqHeroesArchiveEntry.Size);
        Span<byte> entryBuffer = entryBufferLength <= MaxStackAllocLimit ? stackalloc byte[entryBufferLength] : new byte[entryBufferLength]; // get the entry table buffer
        stream.Position = (int)_mpqHeader.BlockTablePos;
        stream.Read(entryBuffer);

        DecryptTable(entryBuffer, "(block table)");

        _mpqArchiveEntries = new MpqHeroesArchiveEntry[_mpqHeader.BlockTableSize];
        BitReader mpqArchivesEntryBitReader = new(entryBuffer, EndianType.LittleEndian);

        for (int i = 0; i < _mpqHeader.BlockTableSize; i++)
            _mpqArchiveEntries[i] = new MpqHeroesArchiveEntry(ref mpqArchivesEntryBitReader, (uint)_mpqHeader.HeaderOffset);

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
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
#else
        if (size < 1)
            throw new ArgumentOutOfRangeException(nameof(size));
#endif

        Span<byte> data = new byte[size];
        _archiveStream.Position = 0;
        _archiveStream.Read(data);

        MemoryStream stream = new();
        stream.Write(data);
        stream.Position = 0;

        return stream;
    }

    /// <summary>
    /// Gets the headers in bytes.
    /// </summary>
    /// <param name="buffer">A span to read in the bytes.</param>
    public void GetHeaderBytes(Span<byte> buffer)
    {
        _archiveStream.Position = 0;
        _archiveStream.Read(buffer);
    }

    /// <summary>
    /// Gets a <see cref="MpqHeroesArchiveEntry"/> by its file name.
    /// </summary>
    /// <param name="fileName">The name of the archive entry.</param>
    /// <returns>An <see cref="MpqHeroesArchiveEntry"/>.</returns>
    /// <exception cref="FileNotFoundException">The <paramref name="fileName"/> was not found.</exception>
    public MpqHeroesArchiveEntry GetEntry(string fileName)
    {
        if (!TryGetEntry(fileName, out MpqHeroesArchiveEntry? entry))
            throw new FileNotFoundException("File not found: " + fileName);

        return entry.Value;
    }

    /// <summary>
    /// Tries to get a <see cref="MpqHeroesArchiveEntry"/> by its file name.
    /// </summary>
    /// <param name="fileName">The name of the archive entry.</param>
    /// <param name="mpqHeroesArchiveEntry">When this method returns, contains the <see cref="MpqHeroesArchiveEntry"/>.</param>
    /// <returns><see langword="true"/> if the value was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetEntry(string fileName, [NotNullWhen(true)] out MpqHeroesArchiveEntry? mpqHeroesArchiveEntry)
    {
        mpqHeroesArchiveEntry = null;

        if (!TryGetHashEntry(fileName, out MpqHash hash))
            return false;

        MpqHeroesArchiveEntry entry = _mpqArchiveEntries[hash.BlockIndex];

        mpqHeroesArchiveEntry = entry;

        return true;
    }

    /// <summary>
    /// Checks if the entry exist.
    /// </summary>
    /// <param name="fileName">The name of the archive entry. Is case-insensitive.</param>
    /// <returns><see langword="true"/> if the entry exists, otherwise returns <see langword="false"/>.</returns>
    public bool FileEntryExists(string fileName) => TryGetHashEntry(fileName, out _);

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
            int blockPositionCount = (int)((mpqArchiveEntry.FileSize + _blockSize - 1) / _blockSize) + 1;

            // Files with metadata have an extra block containing block checksums
            if ((mpqArchiveEntry.Flags & MpqFileFlags.FileHasMetadata) != 0)
                blockPositionCount++;

            int blockPositionByteLength = 4 * blockPositionCount;
            Span<uint> blockPositionsUint = blockPositionCount <= MaxStackAllocLimit ? stackalloc uint[blockPositionCount] : new uint[blockPositionCount];
            Span<byte> blockPositionByte = blockPositionByteLength <= MaxStackAllocLimit ? stackalloc byte[blockPositionByteLength] : new byte[blockPositionByteLength];

            _archiveStream.Seek(mpqArchiveEntry.FilePosition, SeekOrigin.Begin);
            _archiveStream.Read(blockPositionByte);

            SetBlockPositions(blockPositionByte, blockPositionsUint, blockPositionCount);

            if (mpqArchiveEntry.IsEncrypted)
            {
                throw new MpqHeroesToolException("The entry is encrypted.");
            }

            int toRead = (int)mpqArchiveEntry.FileSize;
            int offset = 0;
            int position = 0;

            while (toRead > offset)
            {
                int sizeOfBlock = LoadBlockPositions(mpqArchiveEntry, blockPositionsUint, position, buffer[offset..]);

                if (sizeOfBlock < 1)
                    break;

                offset += sizeOfBlock;
                position = offset / _blockSize;
            }
        }
        else
        {
            _archiveStream.Position = mpqArchiveEntry.FilePosition;

            int read = _archiveStream.Read(buffer[..(int)mpqArchiveEntry.CompressedSize]);

            if (read != mpqArchiveEntry.CompressedSize)
                throw new MpqHeroesToolException("Insufficient data or invalid data length");

            if (mpqArchiveEntry.CompressedSize != mpqArchiveEntry.FileSize)
            {
                DecompressByteData(buffer);
            }
        }
    }

    /// <summary>
    /// Decompresses an <see cref="MpqHeroesArchiveEntry"/> and returns it as a stream.
    /// </summary>
    /// <param name="mpqArchiveEntry">The <see cref="MpqHeroesArchiveEntry"/> to decompress.</param>
    /// <returns>A stream of the contents of the entry.</returns>
    public Stream DecompressEntry(MpqHeroesArchiveEntry mpqArchiveEntry)
    {
        Span<byte> buffer = mpqArchiveEntry.FileSize <= MaxStackAllocLimit ? stackalloc byte[(int)mpqArchiveEntry.FileSize] : new byte[(int)mpqArchiveEntry.FileSize];
        DecompressEntry(mpqArchiveEntry, buffer);

        MemoryStream stream = new();
        stream.Write(buffer);
        stream.Position = 0;

        return stream;
    }

    internal static uint HashString(ReadOnlySpan<char> input, int offset)
    {
        Span<char> upperInput = input.Length <= MaxStackAllocLimit ? stackalloc char[input.Length] : new char[input.Length];

        uint seed1 = 0x7fed7fed;
        uint seed2 = 0xeeeeeeee;

        input.ToUpperInvariant(upperInput);

        for (int i = 0; i < upperInput.Length; i++)
        {
            seed1 = _stormBuffer[offset + upperInput[i]] ^ seed1 + seed2;
            seed2 = upperInput[i] + seed1 + seed2 + (seed2 << 5) + 3;
        }

        return seed1;
    }

    internal static void DecryptBlock(uint[] data, uint seed1)
    {
        uint seed2 = 0xeeeeeeee;

        for (int i = 0; i < data.Length; i++)
        {
            seed2 += _stormBuffer[0x400 + (seed1 & 0xff)];
            uint result = data[i];
            result ^= seed1 + seed2;

            seed1 = (~seed1 << 21) + 0x11111111 | seed1 >> 11;
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
            uint result = value0 ^ seed1 + seed2;

            if (result != decrypted)
                continue;

            uint saveseed1 = seed1;

            // Test this result against the 2nd value
            seed1 = (~seed1 << 21) + 0x11111111 | seed1 >> 11;
            seed2 = result + seed2 + (seed2 << 5) + 3;

            seed2 += _stormBuffer[0x400 + (seed1 & 0xff)];
            result = value1 ^ seed1 + seed2;

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

                result[index2] = temp | seed & 0xffff;
            }
        }

        return result;
    }

    private static void DecryptBlock(Span<byte> data, uint seed1)
    {
        uint seed2 = 0xeeeeeeee;

        // NB: If the block is not an even multiple of 4,
        // the remainder is not encrypted
        for (int i = 0; i < data.Length - 3; i += 4)
        {
            seed2 += _stormBuffer[0x400 + (seed1 & 0xff)];

            uint result = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i, 4));

            result ^= seed1 + seed2;

            seed1 = (~seed1 << 21) + 0x11111111 | seed1 >> 11;
            seed2 = result + seed2 + (seed2 << 5) + 3;

            data[i + 0] = (byte)(result & 0xff);
            data[i + 1] = (byte)(result >> 8 & 0xff);
            data[i + 2] = (byte)(result >> 16 & 0xff);
            data[i + 3] = (byte)(result >> 24 & 0xff);
        }
    }

    private static void DecryptTable(Span<byte> data, string key)
    {
        DecryptBlock(data, HashString(key, 0x300));
    }

    private static void DecompressByteData(Span<byte> buffer)
    {
        byte compressionType = buffer[0];

        switch (compressionType)
        {
            case 1: // Huffman
                throw new MpqHeroesToolException("Huffman not yet supported");
            case 2: // ZLib/Deflate
                ZlibDecompress(buffer);
                break;
            case 8: // PKLib/Impode
                throw new MpqHeroesToolException("PKLib/Impode not yet supported");
            case 0x10:
                BZip2Decompress(buffer);
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

    private static void BZip2Decompress(Span<byte> buffer)
    {
        using MemoryStream memoryStream = new();
        memoryStream.Write(buffer[1..]);
        memoryStream.Position = 0;

        using BZip2InputStream stream = new(memoryStream);

        stream.Read(buffer);
    }

    private static void ZlibDecompress(Span<byte> buffer)
    {
        using MemoryStream memoryStream = new();
        memoryStream.Write(buffer[1..]);
        memoryStream.Position = 0;

        using ZlibStream stream = new(memoryStream, CompressionMode.Decompress);

        stream.Read(buffer);
    }

    private static void SetBlockPositions(ReadOnlySpan<byte> source, Span<uint> blockPositions, int blockPositionCount)
    {
        BitReader bitReader = new(source, EndianType.LittleEndian);

        for (int i = 0; i < blockPositionCount; i++)
        {
            blockPositions[i] = bitReader.ReadUInt32Aligned();
        }
    }

    private static string GetFileName(ReadOnlySpan<byte> source, int startIndex, ref int index, byte charByte)
    {
        Span<char> data = stackalloc char[index - startIndex];

        Encoding.UTF8.GetChars(source[startIndex..index], data);

        // if it's a \r, check one ahead for a \n
        if (charByte == 13 && index < source.Length)
        {
            byte nByte = source[index + 1];
            if (nByte == 10)
            {
                index++;
            }
        }

        return data.ToString();
    }

    private void AddListfileFileNames()
    {
        if (!AddFileName("(listfile)"))
            return;

        MpqHeroesArchiveEntry entry = GetEntry("(listfile)");

        Span<byte> buffer = entry.FileSize <= MaxStackAllocLimit ? stackalloc byte[(int)entry.FileSize] : new byte[(int)entry.FileSize];
        DecompressEntry(entry, buffer);

        AddFileNames(buffer);
    }

    private void AddFileNames(ReadOnlySpan<byte> source)
    {
        int startIndex = 0;
        int index = 0;

        do
        {
            byte charByte = source[index];

            // \n - UNIX   \r\n - DOS   \r - Mac
            if (charByte == 10 || charByte == 13)
            {
                string fileName = GetFileName(source, startIndex, ref index, charByte);

                index++;

                AddFileName(fileName);
                startIndex = index;
            }

            index++;
        }
        while (index < source.Length);
    }

    private bool AddFileName(string fileName)
    {
        if (!TryGetHashEntry(fileName, out MpqHash hash)) return false;

        _mpqArchiveEntries[hash.BlockIndex].FileName = fileName;
        return true;
    }

    private int LoadBlockPositions(MpqHeroesArchiveEntry mpqArchiveEntry, ReadOnlySpan<uint> blockPositions, int position, Span<byte> buffer)
    {
        int expectedlength = (int)Math.Min(mpqArchiveEntry.FileSize - (position * _blockSize), _blockSize);
        if (expectedlength < 1)
            return 0;

        return LoadBlock(mpqArchiveEntry, blockPositions, position, expectedlength, buffer);
    }

    private int LoadBlock(MpqHeroesArchiveEntry mpqArchiveEntry, ReadOnlySpan<uint> blockPositions, int blockIndex, int expectedLength, Span<byte> buffer)
    {
        uint offset;
        int toRead;
        uint encryptionSeed;

        if (mpqArchiveEntry.IsCompressed)
        {
            offset = blockPositions[blockIndex];
            toRead = (int)(blockPositions[blockIndex + 1] - offset);
        }
        else
        {
            offset = (uint)(blockIndex * _blockSize);
            toRead = expectedLength;
        }

        offset += mpqArchiveEntry.FilePosition;

        _archiveStream.Seek(offset, SeekOrigin.Begin);
        int read = _archiveStream.Read(buffer[..toRead]);

        if (read != toRead)
            throw new MpqHeroesToolException("Insufficient data or invalid data length");

        if (mpqArchiveEntry.IsEncrypted && mpqArchiveEntry.FileSize > 3)
        {
            if (mpqArchiveEntry.EncryptionSeed == 0)
                throw new MpqHeroesToolException("Unable to determine encryption key");

            encryptionSeed = (uint)(blockIndex + mpqArchiveEntry.EncryptionSeed);
            DecryptBlock(buffer[..expectedLength], encryptionSeed);
        }

        if (mpqArchiveEntry.IsCompressed && toRead != expectedLength)
        {
            if ((mpqArchiveEntry.Flags & MpqFileFlags.CompressedMulti) != 0)
                DecompressByteData(buffer[..expectedLength]);
            else
                throw new MpqHeroesToolException("Non-single unit must be decompresssed using pk");
        }

        return expectedLength;
    }

    private bool TryGetHashEntry(ReadOnlySpan<char> filename, out MpqHash hash)
    {
        uint index = HashString(filename, 0);
        index &= _mpqHeader.HashTableSize - 1;
        uint name1 = HashString(filename, 0x100);
        uint name2 = HashString(filename, 0x200);

        for (uint i = index; i < _mpqHashes.Length; ++i)
        {
            hash = _mpqHashes[i];
            if (hash.Name1 == name1 && hash.Name2 == name2)
                return true;
        }

        for (uint i = 0; i < index; i++)
        {
            hash = _mpqHashes[i];
            if (hash.Name1 == name1 && hash.Name2 == name2)
                return true;
        }

        hash = new MpqHash(0, 0, 0, 0);
        return false;
    }
}

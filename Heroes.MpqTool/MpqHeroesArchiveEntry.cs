namespace Heroes.MpqTool;

/// <summary>
/// An entry of a mpq archive.
/// </summary>
public struct MpqHeroesArchiveEntry
{
    internal const uint Size = 16;

    private readonly uint _fileOffset; // Relative to the header offset

    private string? _fileName;

    internal MpqHeroesArchiveEntry(ref BitReader bitReaderStruct, uint headerOffset)
    {
        _fileOffset = bitReaderStruct.ReadUInt32Aligned();
        FilePosition = headerOffset + _fileOffset;
        CompressedSize = bitReaderStruct.ReadUInt32Aligned();
        FileSize = bitReaderStruct.ReadUInt32Aligned();
        Flags = (MpqFileFlags)bitReaderStruct.ReadUInt32Aligned();
        EncryptionSeed = 0;
        _fileName = null;
    }

    /// <summary>
    /// Gets the compressed size of the entry in bytes.
    /// </summary>
    public uint CompressedSize { get; }

    /// <summary>
    /// Gets the uncompressed size of the entry in bytes.
    /// </summary>
    public uint FileSize { get; }

    /// <summary>
    /// Gets the file flags.
    /// </summary>
    public MpqFileFlags Flags { get; }

    /// <summary>
    /// Gets the encryption seed.
    /// </summary>
    public uint EncryptionSeed { get; internal set; }

    /// <summary>
    /// Gets the name of the entry.
    /// </summary>
    public string? FileName
    {
        readonly get
        {
            return _fileName;
        }

        internal set
        {
            _fileName = value;
            EncryptionSeed = CalculateEncryptionSeed();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the entry is encrypted.
    /// </summary>
    public readonly bool IsEncrypted => (Flags & MpqFileFlags.Encrypted) != 0;

    /// <summary>
    /// Gets a value indicating whether the entry is compressed.
    /// </summary>
    public readonly bool IsCompressed => (Flags & MpqFileFlags.Compressed) != 0;

    /// <summary>
    /// Gets a value indicating whether the entry exists and is valid.
    /// </summary>
    public readonly bool Exists => Flags != 0;

    /// <summary>
    /// Gets a value indicating whether the entry.
    /// </summary>
    public readonly bool IsSingleUnit => (Flags & MpqFileFlags.SingleUnit) != 0;

    internal uint FilePosition { get; } // Absolute position in the file

    /// <inheritdoc/>
    public override readonly string ToString()
    {
        if (FileName is null)
        {
            if (!Exists)
                return "(Deleted file)";
            return string.Format("Unknown file @ {0}", FilePosition);
        }

        return FileName;
    }

    private readonly uint CalculateEncryptionSeed()
    {
        if (FileName is null) return 0;

        uint seed = MpqHeroesArchive.HashString(Path.GetFileName(FileName), 0x300);
        if ((Flags & MpqFileFlags.BlockOffsetAdjustedKey) == MpqFileFlags.BlockOffsetAdjustedKey)
            seed = seed + _fileOffset ^ FileSize;
        return seed;
    }
}

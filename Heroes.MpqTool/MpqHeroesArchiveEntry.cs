namespace Heroes.MpqTool;

/// <summary>
/// An entry of a mpq archive.
/// </summary>
public struct MpqHeroesArchiveEntry
{
    private readonly uint _fileOffset; // Relative to the header offset

    // blockTableEntry is the four values of the entry in the decrypted block table.
    internal MpqHeroesArchiveEntry(ReadOnlySpan<uint> blockTableEntry, uint headerOffset)
    {
        _fileOffset = blockTableEntry[0];
        FilePosition = headerOffset + _fileOffset;
        CompressedSize = blockTableEntry[1];
        FileSize = blockTableEntry[2];
        Flags = (MpqFileFlags)blockTableEntry[3];
        FileName = null;
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
    /// Gets the encryption seed. It is derived from the <see cref="FileName"/>, so it is 0 when the name is unknown.
    /// </summary>
    public readonly uint EncryptionSeed => CalculateEncryptionSeed();

    /// <summary>
    /// Gets the name of the entry.
    /// </summary>
    public string? FileName { get; internal set; }

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

    internal uint FilePosition { get; } // Position from the start of the archive

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

        uint seed = MpqHeroesArchive.HashString(Path.GetFileName(FileName.AsSpan()), 0x300);
        if ((Flags & MpqFileFlags.BlockOffsetAdjustedKey) == MpqFileFlags.BlockOffsetAdjustedKey)
            seed = (seed + _fileOffset) ^ FileSize;
        return seed;
    }
}

namespace Heroes.MpqTool;

/// <summary>
/// Specifies the mpq file flags.
/// </summary>
[Flags]
public enum MpqFileFlags : uint
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1602 // Enumeration items should be documented
    CompressedPK = 0x100, // AKA Imploded
    CompressedMulti = 0x200,
    Compressed = 0xff00,
    Encrypted = 0x10000,
    BlockOffsetAdjustedKey = 0x020000, // AKA FixSeed
    SingleUnit = 0x1000000,
    FileHasMetadata = 0x04000000,
    Exists = 0x80000000,
#pragma warning restore SA1602 // Enumeration items should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

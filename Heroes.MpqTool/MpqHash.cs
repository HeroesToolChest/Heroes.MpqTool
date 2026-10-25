namespace Heroes.MpqTool;

internal readonly struct MpqHash
{
    // The block index of a hash table slot that has never been used. A lookup stops when it reaches one.
    public const uint EmptyBlockIndex = 0xFFFFFFFF;

    private readonly uint _name1;
    private readonly uint _name2;
    private readonly uint _locale; // Normally 0 or UInt32.MaxValue (0xffffffff)
    private readonly uint _blockIndex;

    public MpqHash(uint name1, uint name2, uint locale, uint blockIndex)
    {
        _name1 = name1;
        _name2 = name2;
        _locale = locale;
        _blockIndex = blockIndex;
    }

    public uint Name1 => _name1;

    public uint Name2 => _name2;

    public uint Locale => _locale;

    public uint BlockIndex => _blockIndex;
}

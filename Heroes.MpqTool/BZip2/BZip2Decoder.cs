// BZip2Decoder.cs
// Rewritten from DotNetZip's BZip2InputStream (https://github.com/DinoChiesa/DotNetZip/blob/master/BZip2/BZip2InputStream.cs),
// following the bzip2 reference implementation (libbzip2).
//
// MPQ entries are decompressed in one go: the whole compressed entry is in memory and the decompressed size is known.
// So instead of a general purpose stream, this decodes from a span straight into the destination.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Heroes.MpqTool.BZip2;

/// <summary>
/// Decompresses bzip2 data.
/// </summary>
internal static class BZip2Decoder
{
    private const int BlockSizeMultiple = 100000;
    private const int MaxAlphaSize = 258;
    private const int MaxCodeLength = 20;
    private const int MinGroups = 2;
    private const int MaxGroups = 6;
    private const int GroupSize = 50;
    private const int MaxSelectors = 1 << 15;
    private const int RunB = 1;

    // Huffman codes up to this many bits long are decoded with a single table lookup.
    private const int LookupBits = 10;

    // Blocks at least this long are walked with several chains at once (see TryWalkInParallel). Shorter ones fit in the CPU cache.
    private const int ParallelWalkMinLength = 1 << 16;
    private const int WalkLanes = 8;
    private const int WalkStarts = 64;
    private const int WalkSlotLength = 1 << 12;

    private const ulong BlockMagic = 0x314159265359;
    private const ulong EndOfStreamMagic = 0x177245385090;

    // Slicing-by-8 tables for the (MSB first) CRC-32 that bzip2 uses.
    private static readonly uint[] _crcTables = BuildCrcTables();

    // The intervals between the bytes flipped in a randomised block.
    private static ReadOnlySpan<int> RNums =>
    [
        619, 720, 127, 481, 931, 816, 813, 233, 566, 247,
        985, 724, 205, 454, 863, 491, 741, 242, 949, 214,
        733, 859, 335, 708, 621, 574,  73, 654, 730, 472,
        419, 436, 278, 496, 867, 210, 399, 680, 480,  51,
        878, 465, 811, 169, 869, 675, 611, 697, 867, 561,
        862, 687, 507, 283, 482, 129, 807, 591, 733, 623,
        150, 238,  59, 379, 684, 877, 625, 169, 643, 105,
        170, 607, 520, 932, 727, 476, 693, 425, 174, 647,
        73, 122, 335, 530, 442, 853, 695, 249, 445, 515,
        909, 545, 703, 919, 874, 474, 882, 500, 594, 612,
        641, 801, 220, 162, 819, 984, 589, 513, 495, 799,
        161, 604, 958, 533, 221, 400, 386, 867, 600, 782,
        382, 596, 414, 171, 516, 375, 682, 485, 911, 276,
        98, 553, 163, 354, 666, 933, 424, 341, 533, 870,
        227, 730, 475, 186, 263, 647, 537, 686, 600, 224,
        469,  68, 770, 919, 190, 373, 294, 822, 808, 206,
        184, 943, 795, 384, 383, 461, 404, 758, 839, 887,
        715,  67, 618, 276, 204, 918, 873, 777, 604, 560,
        951, 160, 578, 722,  79, 804,  96, 409, 713, 940,
        652, 934, 970, 447, 318, 353, 859, 672, 112, 785,
        645, 863, 803, 350, 139,  93, 354,  99, 820, 908,
        609, 772, 154, 274, 580, 184,  79, 626, 630, 742,
        653, 282, 762, 623, 680,  81, 927, 626, 789, 125,
        411, 521, 938, 300, 821,  78, 343, 175, 128, 250,
        170, 774, 972, 275, 999, 639, 495,  78, 352, 126,
        857, 956, 358, 619, 580, 124, 737, 594, 701, 612,
        669, 112, 134, 694, 363, 992, 809, 743, 168, 974,
        944, 375, 748,  52, 600, 747, 642, 182, 862,  81,
        344, 805, 988, 739, 511, 655, 814, 334, 249, 515,
        897, 955, 664, 981, 649, 113, 974, 459, 893, 228,
        433, 837, 553, 268, 926, 240, 102, 654, 459, 51,
        686, 754, 806, 760, 493, 403, 415, 394, 687, 700,
        946, 670, 656, 610, 738, 392, 760, 799, 887, 653,
        978, 321, 576, 617, 626, 502, 894, 679, 243, 440,
        680, 879, 194, 572, 640, 724, 926,  56, 204, 700,
        707, 151, 457, 449, 797, 195, 791, 558, 945, 679,
        297,  59,  87, 824, 713, 663, 412, 693, 342, 606,
        134, 108, 571, 364, 631, 212, 174, 643, 304, 329,
        343,  97, 430, 751, 497, 314, 983, 374, 822, 928,
        140, 206,  73, 263, 980, 736, 876, 478, 430, 305,
        170, 514, 364, 692, 829,  82, 855, 953, 676, 246,
        369, 970, 294, 750, 807, 827, 150, 790, 288, 923,
        804, 378, 215, 828, 592, 281, 565, 555, 710,  82,
        896, 831, 547, 261, 524, 462, 293, 465, 502,  56,
        661, 821, 976, 991, 658, 869, 905, 758, 745, 193,
        768, 550, 608, 933, 378, 286, 215, 979, 792, 961,
        61, 688, 793, 644, 986, 403, 106, 366, 905, 644,
        372, 567, 466, 434, 645, 210, 389, 550, 919, 135,
        780, 773, 635, 389, 707, 100, 626, 958, 165, 504,
        920, 176, 193, 713, 857, 265, 203,  50, 668, 108,
        645, 990, 626, 197, 510, 357, 358, 850, 858, 364,
        936, 638,
    ];

    /// <summary>
    /// Decompresses bzip2 data into <paramref name="destination"/>, filling it completely.
    /// </summary>
    /// <remarks>
    /// If the data decompresses to more than <paramref name="destination"/> can hold, the rest is ignored.
    /// </remarks>
    /// <param name="source">The bzip2 data, starting with the "BZh" signature.</param>
    /// <param name="destination">The buffer to write the decompressed data to.</param>
    /// <exception cref="IOException">The data is not valid bzip2 data, or it decompresses to less than <paramref name="destination"/>.</exception>
    public static void Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        BitInput input = new(source);

        if (input.GetBits(24) != 0x425A68) // "BZh"
            throw new IOException("Not a valid BZip2 stream.");

        int level = (int)input.GetBits(8) - '0';

        if (level < 1 || level > 9)
            throw new IOException("Not a valid BZip2 stream: illegal block size.");

        int maxBlockLength = level * BlockSizeMultiple;

        DecoderTables tables = DecoderTables.Get();

        // A block holds the run-length encoded data, which is at most 5/4 the size of the decompressed data,
        // so for small entries the block buffer can be smaller than the maximum the level allows. It grows if it has to.
        int[] tt = ArrayPool<int>.Shared.Rent((int)Math.Min(maxBlockLength, (destination.Length * 5L / 4) + 16));

        try
        {
            uint combinedCrc = 0;
            int written = 0;

            while (true)
            {
                ulong magic = ((ulong)input.GetBits(24) << 24) | input.GetBits(24);

                if (magic == EndOfStreamMagic)
                {
                    uint storedCombinedCrc = input.GetBits(32);

                    if (written < destination.Length)
                        throw new IOException("The BZip2 data ended before the destination was filled.");

                    if (storedCombinedCrc != combinedCrc)
                        throw new IOException($"BZip2 CRC error (expected {storedCombinedCrc:X8}, computed {combinedCrc:X8})");

                    return;
                }

                if (magic != BlockMagic)
                    throw new IOException("Bad BZip2 block header.");

                // the destination is full, so the rest isn't needed
                if (written == destination.Length)
                    return;

                uint storedBlockCrc = input.GetBits(32);
                bool randomised = input.GetBits(1) != 0;
                int origPtr = (int)input.GetBits(24);
                int blockLength = ReadBlock(ref input, tables, ref tt, maxBlockLength);

                if (origPtr >= blockLength)
                    throw new IOException("BZip2 data error: the original pointer is out of range.");

                Span<byte> blockDestination = destination[written..];
                int blockWritten = UndoTransforms(tt.AsSpan(0, blockLength), tables.ByteCounts, origPtr, randomised, blockDestination, out bool complete);

                // the destination filled up part way through the block, so the block's CRC can't be checked
                if (!complete)
                    return;

                uint blockCrc = ~UpdateCrc(uint.MaxValue, blockDestination[..blockWritten]);

                if (blockCrc != storedBlockCrc)
                    throw new IOException($"BZip2 CRC error (expected {storedBlockCrc:X8}, computed {blockCrc:X8})");

                combinedCrc = ((combinedCrc << 1) | (combinedCrc >> 31)) ^ blockCrc;
                written += blockWritten;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(tt);
        }
    }

    // Reads the symbols of a block and undoes the run-length (RUNA/RUNB) and move-to-front encodings.
    // The resulting bytes are stored in tt (one per int) and the count of each byte value in tables.ByteCounts.
    // Returns the number of bytes in the block.
    private static int ReadBlock(ref BitInput input, DecoderTables tables, ref int[] tt, int maxBlockLength)
    {
        // The byte values used in the block, as a bitmap of which ranges of 16 are used, followed by a bitmap of each used range.
        // They make up the starting move-to-front list.
        Span<byte> mtf = stackalloc byte[256];
        int inUseCount = 0;
        uint inUse16 = input.GetBits(16);

        for (int i = 0; i < 16; i++)
        {
            if ((inUse16 & (0x8000u >> i)) == 0)
                continue;

            uint inUse = input.GetBits(16);

            for (int j = 0; j < 16; j++)
            {
                if ((inUse & (0x8000u >> j)) != 0)
                    mtf[inUseCount++] = (byte)((i * 16) + j);
            }
        }

        if (inUseCount == 0)
            ThrowDataError("no byte values are used");

        int alphaSize = inUseCount + 2;
        int endOfBlock = inUseCount + 1;

        int groupCount = (int)input.GetBits(3);
        int selectorCount = (int)input.GetBits(15);

        if (groupCount < MinGroups || groupCount > MaxGroups || selectorCount < 1)
            ThrowDataError("invalid Huffman group header");

        ReadSelectors(ref input, tables.Selectors.AsSpan(0, selectorCount), groupCount);

        for (int g = 0; g < groupCount; g++)
        {
            Span<byte> lengths = tables.Lengths.AsSpan(g * MaxAlphaSize, alphaSize);

            ReadCodeLengths(ref input, lengths);
            BuildHuffmanTable(tables, g, lengths);
        }

        tables.AlphaSize = alphaSize;

        Span<int> byteCounts = tables.ByteCounts;
        byteCounts.Clear();

        ReadOnlySpan<byte> selectors = tables.Selectors.AsSpan(0, selectorCount);

        // the bit reader's state is kept in locals so it stays in registers
        ReadOnlySpan<byte> source = input.Source;
        int position = input.Position;
        ulong bits = input.Buffer;
        int bitCount = input.Count;

        int capacity = Math.Min(tt.Length, maxBlockLength);
        Span<int> block = tt.AsSpan(0, capacity);
        int length = 0;

        int nextSelector = 0;
        int group = 0;
        int groupRemaining = 0;
        ReadOnlySpan<ushort> lookup = default;

        int runLength = 0;
        int runWeight = 1;

        while (true)
        {
            // each group of 50 symbols has its own Huffman table
            if (groupRemaining == 0)
            {
                if (nextSelector >= selectors.Length)
                    ThrowDataError("ran out of selectors");

                group = selectors[nextSelector++];
                lookup = tables.Lookup.AsSpan(group << LookupBits, 1 << LookupBits);
                groupRemaining = GroupSize;
            }

            groupRemaining--;

            if (bitCount < MaxCodeLength)
                Refill(source, ref position, ref bits, ref bitCount);

            int symbol;
            int codeLength;
            int entry = lookup[(int)(bits >> (64 - LookupBits))];

            if (entry != 0)
            {
                symbol = entry >> 5;
                codeLength = entry & 0x1F;
            }
            else
            {
                symbol = DecodeLongCode(tables, group, bits, out codeLength);
            }

            if (codeLength > bitCount)
                ThrowEndOfData();

            bits <<= codeLength;
            bitCount -= codeLength;

            if (symbol <= RunB)
            {
                // RUNA and RUNB give the length of a run of the front byte, as digits 1 and 2 in bijective base 2
                runLength += runWeight << symbol;
                runWeight <<= 1;

                if (runLength > maxBlockLength)
                    ThrowDataError("run is longer than a block");

                continue;
            }

            if (runLength > 0)
            {
                if (runLength > capacity - length)
                    Grow(ref tt, ref block, ref capacity, length, length + runLength, maxBlockLength);

                byte front = mtf[0];
                block.Slice(length, runLength).Fill(front);
                byteCounts[front] += runLength;
                length += runLength;

                runLength = 0;
                runWeight = 1;
            }

            if (symbol == endOfBlock)
                break;

            // the symbol is a position in the move-to-front list (plus one)
            byte value = MoveToFront(mtf, symbol - 1);

            if (length == capacity)
                Grow(ref tt, ref block, ref capacity, length, length + 1, maxBlockLength);

            block[length++] = value;
            byteCounts[value]++;
        }

        input.Position = position;
        input.Buffer = bits;
        input.Count = bitCount;

        return length;
    }

    // Each selector picks the Huffman table of a group of 50 symbols. They're move-to-front encoded and stored in unary.
    private static void ReadSelectors(ref BitInput input, Span<byte> selectors, int groupCount)
    {
        Span<byte> order = [0, 1, 2, 3, 4, 5];

        for (int i = 0; i < selectors.Length; i++)
        {
            int index = 0;

            while (input.GetBits(1) != 0)
            {
                if (++index >= groupCount)
                    ThrowDataError("invalid selector");
            }

            byte group = order[index];

            for (; index > 0; index--)
                order[index] = order[index - 1];

            order[0] = group;
            selectors[i] = group;
        }
    }

    // The code lengths of a Huffman table are delta encoded.
    private static void ReadCodeLengths(ref BitInput input, Span<byte> lengths)
    {
        int length = (int)input.GetBits(5);

        for (int i = 0; i < lengths.Length; i++)
        {
            while (true)
            {
                if (length < 1 || length > MaxCodeLength)
                    ThrowDataError("invalid code length");

                if (input.GetBits(1) == 0)
                    break;

                length += input.GetBits(1) == 0 ? 1 : -1;
            }

            lengths[i] = (byte)length;
        }
    }

    // bzip2 uses canonical Huffman codes: shorter codes come first, and codes of the same length are in symbol order.
    private static void BuildHuffmanTable(DecoderTables tables, int group, ReadOnlySpan<byte> lengths)
    {
        Span<int> lengthCounts = stackalloc int[MaxCodeLength + 1];
        Span<int> nextCode = stackalloc int[MaxCodeLength + 1];
        Span<int> nextIndex = stackalloc int[MaxCodeLength + 1];

        foreach (byte length in lengths)
            lengthCounts[length]++;

        Span<int> limit = tables.Limit.AsSpan(group * (MaxCodeLength + 1), MaxCodeLength + 1);
        Span<int> codeBase = tables.Base.AsSpan(group * (MaxCodeLength + 1), MaxCodeLength + 1);

        int code = 0;
        int index = 0;
        int maxLength = 0;

        for (int length = 1; length <= MaxCodeLength; length++)
        {
            int count = lengthCounts[length];

            if (count > 0)
                maxLength = length;

            nextCode[length] = code;
            nextIndex[length] = index;
            codeBase[length] = code - index;
            limit[length] = code + count - 1;

            code += count;
            index += count;

            if (code > (1 << length))
                ThrowDataError("invalid Huffman code lengths");

            code <<= 1;
        }

        tables.MaxLengths[group] = maxLength;

        Span<int> perm = tables.Perm.AsSpan(group * MaxAlphaSize, MaxAlphaSize);
        Span<ushort> lookup = tables.Lookup.AsSpan(group << LookupBits, 1 << LookupBits);
        lookup.Clear();

        for (int symbol = 0; symbol < lengths.Length; symbol++)
        {
            int length = lengths[symbol];

            perm[nextIndex[length]++] = symbol;

            int symbolCode = nextCode[length]++;

            // every lookup entry that starts with this code decodes to this symbol
            if (length <= LookupBits)
            {
                int shift = LookupBits - length;
                lookup.Slice(symbolCode << shift, 1 << shift).Fill((ushort)((symbol << 5) | length));
            }
        }
    }

    // Decodes a symbol whose code is longer than LookupBits.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int DecodeLongCode(DecoderTables tables, int group, ulong bits, out int codeLength)
    {
        ReadOnlySpan<int> limit = tables.Limit.AsSpan(group * (MaxCodeLength + 1), MaxCodeLength + 1);
        ReadOnlySpan<int> codeBase = tables.Base.AsSpan(group * (MaxCodeLength + 1), MaxCodeLength + 1);
        int maxLength = tables.MaxLengths[group];

        for (int length = LookupBits + 1; length <= maxLength; length++)
        {
            int code = (int)(bits >> (64 - length));

            if (code <= limit[length])
            {
                int index = code - codeBase[length];

                if ((uint)index >= (uint)tables.AlphaSize)
                    break;

                codeLength = length;
                return tables.Perm[(group * MaxAlphaSize) + index];
            }
        }

        ThrowDataError("invalid Huffman code");
        codeLength = 0;
        return 0;
    }

    // Undoes the Burrows-Wheeler transform (and the randomisation, if any) and then the initial run-length encoding of a block,
    // writing the result to destination.
    // Returns the number of bytes written. complete is false if the destination filled up before the end of the block.
    private static int UndoTransforms(Span<int> tt, ReadOnlySpan<int> byteCounts, int origPtr, bool randomised, Span<byte> destination, out bool complete)
    {
        // cftab[b] is the number of bytes in the block that are less than b
        Span<int> cftab = stackalloc int[256];
        int sum = 0;

        for (int i = 0; i < 256; i++)
        {
            cftab[i] = sum;
            sum += byteCounts[i];
        }

        // Link each position to the next one in the upper 24 bits, keeping the byte in the lower 8 bits.
        // (A block is at most 900000 long, so the link only needs 20 bits, which leaves the top bit free for TryWalkInParallel.)
        for (int i = 0; i < tt.Length; i++)
        {
            int b = tt[i] & 0xFF;
            tt[cftab[b]++] |= i << 8;
        }

        int first = tt[origPtr] >>> 8;
        int slotCount = WalkStarts + (tt.Length / WalkSlotLength) + 1;
        bool walkInParallel = tt.Length >= ParallelWalkMinLength && !randomised;
        byte[] walked = ArrayPool<byte>.Shared.Rent(walkInParallel ? slotCount * WalkSlotLength : tt.Length);

        try
        {
            RunLengthDecoder decoder = new(destination);

            if (!walkInParallel || !TryWalkInParallel(tt, first, walked, slotCount, ref decoder))
            {
                Walk(tt, first, walked.AsSpan(0, tt.Length));

                if (randomised)
                    Derandomise(walked.AsSpan(0, tt.Length));

                decoder.Decode(walked.AsSpan(0, tt.Length));
            }

            complete = !decoder.Full;
            return decoder.Written;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(walked);
        }
    }

    // Follows the links through the block from its first byte, writing out the bytes.
    private static void Walk(ReadOnlySpan<int> tt, int position, Span<byte> walked)
    {
        for (int i = 0; i < walked.Length; i++)
        {
            int entry = tt[position];
            walked[i] = (byte)entry;
            position = entry >>> 8;
        }
    }

    // Walking a large block one link at a time is slow, because each step waits on the previous step's load, which is usually
    // a cache miss. So this walks several chains at once: chains start at positions spread through the block (marked with the
    // top bit), and each one runs until it reaches the start of another. The bytes go into fixed size slots that are linked in
    // order, and then run-length decoded in that order.
    // Returns false, without decoding anything, if the chains don't make one cycle through the whole block. That only happens
    // for a periodic block (like "abab...ab"), which Walk handles by going round its shorter cycle more than once.
    private static bool TryWalkInParallel(Span<int> tt, int first, byte[] walked, int slotCount, ref RunLengthDecoder decoder)
    {
        // Slots 0 to WalkStarts - 1 are where the chains start, and the rest are for chains that fill their slot.
        // Slot 0 starts at the first byte of the block.
        Span<int> starts = stackalloc int[WalkStarts];
        Span<int> slotLengths = stackalloc int[slotCount];
        Span<int> nextSlots = stackalloc int[slotCount];

        starts[0] = first;

        for (int i = 1; i < WalkStarts; i++)
        {
            int start = (int)((long)i * tt.Length / WalkStarts);
            starts[i] = start == first ? start + 1 : start;
        }

        foreach (int start in starts)
            tt[start] |= int.MinValue;

        Span<int> lanePositions = stackalloc int[WalkLanes];
        Span<int> laneWrites = stackalloc int[WalkLanes];
        Span<int> laneSlots = stackalloc int[WalkLanes];

        int nextStart = 0;
        int nextSlot = WalkStarts;
        int activeLanes = 0;
        bool oneCycle = true;

        for (int lane = 0; lane < WalkLanes; lane++)
        {
            laneSlots[lane] = nextStart;
            lanePositions[lane] = StartChain(tt, starts[nextStart], walked, nextStart, out laneWrites[lane]);
            nextStart++;
            activeLanes++;
        }

        while (activeLanes > 0 && oneCycle)
        {
            for (int lane = 0; lane < WalkLanes; lane++)
            {
                int slot = laneSlots[lane];

                if (slot < 0)
                    continue;

                int position = lanePositions[lane];
                int entry = tt[position];

                if (entry < 0)
                {
                    // reached the start of another chain
                    slotLengths[slot] = laneWrites[lane] - (slot * WalkSlotLength);
                    nextSlots[slot] = starts.IndexOf(position);

                    if (nextStart < WalkStarts)
                    {
                        laneSlots[lane] = nextStart;
                        lanePositions[lane] = StartChain(tt, starts[nextStart], walked, nextStart, out laneWrites[lane]);
                        nextStart++;
                    }
                    else
                    {
                        laneSlots[lane] = -1;
                        activeLanes--;
                    }

                    continue;
                }

                int write = laneWrites[lane];
                walked[write++] = (byte)entry;
                lanePositions[lane] = entry >>> 8;

                // when the slot is full, the chain carries on in a new one
                if ((write & (WalkSlotLength - 1)) == 0)
                {
                    // There are enough slots for the whole block, so running out means a chain is going round a cycle
                    // without any starts on it.
                    if (nextSlot == slotCount)
                    {
                        oneCycle = false;
                        break;
                    }

                    slotLengths[slot] = WalkSlotLength;
                    nextSlots[slot] = nextSlot;
                    laneSlots[lane] = nextSlot;
                    write = nextSlot * WalkSlotLength;
                    nextSlot++;
                }

                laneWrites[lane] = write;
            }
        }

        foreach (int start in starts)
            tt[start] &= int.MaxValue;

        if (!oneCycle)
            return false;

        // the slots must link up into one cycle covering the whole block
        int total = 0;
        int linked = 0;
        int current = 0;

        do
        {
            total += slotLengths[current];
            current = nextSlots[current];
            linked++;
        }
        while (current != 0 && linked < nextSlot);

        if (current != 0 || linked != nextSlot || total != tt.Length)
            return false;

        do
        {
            if (!decoder.Decode(walked.AsSpan(current * WalkSlotLength, slotLengths[current])))
                break;

            current = nextSlots[current];
        }
        while (current != 0);

        return true;
    }

    // Old versions of bzip2 (and some other encoders) "randomise" blocks they find slow to sort, by flipping the lowest bit of
    // bytes at intervals given by RNums.
    private static void Derandomise(Span<byte> walked)
    {
        ReadOnlySpan<int> rNums = RNums;
        int toGo = 0;
        int next = 0;

        for (int i = 0; i < walked.Length; i++)
        {
            if (toGo == 0)
            {
                toGo = rNums[next];
                next = (next + 1) & 511;
            }

            toGo--;

            if (toGo == 1)
                walked[i] ^= 1;
        }
    }

    // Starts a chain at a (marked) start position by writing out its byte. Returns the next position.
    private static int StartChain(ReadOnlySpan<int> tt, int start, byte[] walked, int slot, out int write)
    {
        int entry = tt[start] & int.MaxValue;

        walked[slot * WalkSlotLength] = (byte)entry;
        write = (slot * WalkSlotLength) + 1;

        return entry >>> 8;
    }

    // Moves list[index] to the front of the 256 byte list and returns it.
    // Replay data is binary, so the positions are often large and this is a hot spot. The list is worked on 16 bytes at a time,
    // always at the same offsets, so each load can be served from the previous call's store. (Moving the bytes up with a
    // memmove reads at offsets one byte off from where the previous call wrote, which stalls.)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte MoveToFront(Span<byte> list, int index)
    {
        if (Avx2.IsSupported)
            return MoveToFrontAvx2(list, index);

        // without a byte shuffle instruction, Vector128.Shuffle is done in software, which is slower than a memmove
        if (!Ssse3.IsSupported && !AdvSimd.IsSupported)
            return MoveToFrontScalar(list, index);

        byte value = list[index];
        Vector128<byte> below = Vector128.Create(value);
        int lastChunk = index >> 4;

        for (int chunk = 0; chunk < lastChunk; chunk++)
        {
            Span<byte> slot = list.Slice(chunk * 16, 16);
            Vector128<byte> current = Vector128.Create<byte>(slot);

            MoveUpOne(current, below).CopyTo(slot);
            below = current;
        }

        // in the 16 bytes holding index, only the bytes up to it move
        Span<byte> lastSlot = list.Slice(lastChunk * 16, 16);
        Vector128<byte> last = Vector128.Create<byte>(lastSlot);
        Vector128<byte> movedLanes = Vector128.LessThanOrEqual(Vector128.Create((byte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15), Vector128.Create((byte)(index & 15)));

        Vector128.ConditionalSelect(movedLanes, MoveUpOne(last, below), last).CopyTo(lastSlot);

        return value;
    }

    private static byte MoveToFrontScalar(Span<byte> list, int index)
    {
        byte value = list[index];

        list[..index].CopyTo(list[1..]);
        list[0] = value;

        return value;
    }

    // MoveToFront, 32 bytes at a time.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte MoveToFrontAvx2(Span<byte> list, int index)
    {
        byte value = list[index];
        Vector256<byte> below = Vector256.Create(value);
        int lastChunk = index >> 5;

        for (int chunk = 0; chunk < lastChunk; chunk++)
        {
            Span<byte> slot = list.Slice(chunk * 32, 32);
            Vector256<byte> current = Vector256.Create<byte>(slot);

            MoveUpOne(current, below).CopyTo(slot);
            below = current;
        }

        Span<byte> lastSlot = list.Slice(lastChunk * 32, 32);
        Vector256<byte> last = Vector256.Create<byte>(lastSlot);
        Vector256<byte> movedLanes = Vector256.LessThanOrEqual(
            Vector256.Create((byte)0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31),
            Vector256.Create((byte)(index & 31)));

        Vector256.ConditionalSelect(movedLanes, MoveUpOne(last, below), last).CopyTo(lastSlot);

        return value;
    }

    // Moves the 32 bytes up one, taking in the last byte of below at the front.
    // AlignRight works on each 16 byte half separately, so the half below each is lined up with Permute2x128 first:
    // below's upper half for the lower half, and the lower half for the upper half.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> MoveUpOne(Vector256<byte> bytes, Vector256<byte> below)
    {
        return Avx2.AlignRight(bytes, Avx2.Permute2x128(bytes, below, 0x03), 15);
    }

    // Moves the bytes up one, taking in the last byte of below at the front.
    // (The shuffle indexes have to be written out here, not in variables, for .NET 8 to use a shuffle instruction.)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> MoveUpOne(Vector128<byte> bytes, Vector128<byte> below)
    {
        return Vector128.Shuffle(bytes, Vector128.Create((byte)0xFF, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14))
            | Vector128.Shuffle(below, Vector128.Create((byte)15, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Grow(ref int[] tt, ref Span<int> block, ref int capacity, int length, int needed, int maxBlockLength)
    {
        if (needed > maxBlockLength)
            ThrowDataError("block overrun");

        int[] larger = ArrayPool<int>.Shared.Rent(Math.Min(maxBlockLength, Math.Max(needed, tt.Length * 2)));
        tt.AsSpan(0, length).CopyTo(larger);
        ArrayPool<int>.Shared.Return(tt);

        tt = larger;
        capacity = Math.Min(tt.Length, maxBlockLength);
        block = tt.AsSpan(0, capacity);
    }

    // Tops the bit buffer up to at least 56 bits, or as many as there are left.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Refill(ReadOnlySpan<byte> source, ref int position, ref ulong bits, ref int bitCount)
    {
        if (position <= source.Length - 8)
        {
            // Bits past the whole bytes added are filled in too, but they're the same bits the next refill adds.
            bits |= BinaryPrimitives.ReadUInt64BigEndian(source[position..]) >> bitCount;
            position += (63 - bitCount) >> 3;
            bitCount |= 56;
        }
        else
        {
            while (bitCount <= 56 && position < source.Length)
            {
                bits |= (ulong)source[position++] << (56 - bitCount);
                bitCount += 8;
            }
        }
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<uint> tables = _crcTables;

        while (data.Length >= 8)
        {
            uint one = BinaryPrimitives.ReadUInt32BigEndian(data) ^ crc;
            uint two = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);

            crc = tables[(7 * 256) + (int)(one >> 24)]
                ^ tables[(6 * 256) + (int)((one >> 16) & 0xFF)]
                ^ tables[(5 * 256) + (int)((one >> 8) & 0xFF)]
                ^ tables[(4 * 256) + (int)(one & 0xFF)]
                ^ tables[(3 * 256) + (int)(two >> 24)]
                ^ tables[(2 * 256) + (int)((two >> 16) & 0xFF)]
                ^ tables[256 + (int)((two >> 8) & 0xFF)]
                ^ tables[(int)(two & 0xFF)];

            data = data[8..];
        }

        foreach (byte b in data)
            crc = (crc << 8) ^ tables[(int)((crc >> 24) ^ b)];

        return crc;
    }

    // tables[(k * 256) + b] is the CRC of byte b followed by k zero bytes.
    private static uint[] BuildCrcTables()
    {
        uint[] tables = new uint[8 * 256];

        for (uint b = 0; b < 256; b++)
        {
            uint crc = b << 24;

            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 0x80000000) != 0 ? (crc << 1) ^ 0x04C11DB7 : crc << 1;

            tables[b] = crc;
        }

        for (int k = 1; k < 8; k++)
        {
            for (int b = 0; b < 256; b++)
            {
                uint previous = tables[((k - 1) * 256) + b];
                tables[(k * 256) + b] = (previous << 8) ^ tables[(int)(previous >> 24)];
            }
        }

        return tables;
    }

    [DoesNotReturn]
    private static void ThrowDataError(string reason) => throw new IOException($"BZip2 data error: {reason}.");

    [DoesNotReturn]
    private static void ThrowEndOfData() => throw new IOException("Unexpected end of BZip2 data.");

    // Undoes the initial run-length encoding: after four equal bytes, the next byte is how many more copies of it follow.
    // The data can be given in pieces.
    private ref struct RunLengthDecoder
    {
        private readonly Span<byte> _destination;
        private int _previous;
        private int _runLength;

        public RunLengthDecoder(Span<byte> destination)
        {
            _destination = destination;
            _previous = -1;
        }

        public int Written { get; private set; }

        // whether the destination filled up before the end of the data
        public bool Full { get; private set; }

        // Returns false if the destination filled up.
        public bool Decode(ReadOnlySpan<byte> source)
        {
            Span<byte> destination = _destination;
            int written = Written;
            int previous = _previous;
            int runLength = _runLength;

            foreach (byte b in source)
            {
                if (runLength == 4)
                {
                    if (b > destination.Length - written)
                    {
                        destination[written..].Fill((byte)previous);
                        written = destination.Length;
                        Full = true;
                        break;
                    }

                    destination.Slice(written, b).Fill((byte)previous);
                    written += b;
                    runLength = 0;
                    continue;
                }

                if (b == previous)
                {
                    runLength++;
                }
                else
                {
                    previous = b;
                    runLength = 1;
                }

                if (written == destination.Length)
                {
                    Full = true;
                    break;
                }

                destination[written++] = b;
            }

            Written = written;
            _previous = previous;
            _runLength = runLength;

            return !Full;
        }
    }

    // Reads bits most significant first.
    private ref struct BitInput
    {
        public BitInput(ReadOnlySpan<byte> source)
        {
            Source = source;
        }

        public ReadOnlySpan<byte> Source { get; }

        // the next byte of Source to add to Buffer
        public int Position { get; set; }

        // the unread bits, left aligned
        public ulong Buffer { get; set; }

        // the number of unread bits in Buffer
        public int Count { get; set; }

        // count is 1 to 32
        public uint GetBits(int count)
        {
            int position = Position;
            ulong bits = Buffer;
            int bitCount = Count;

            if (bitCount < count)
            {
                Refill(Source, ref position, ref bits, ref bitCount);
                Position = position;

                if (bitCount < count)
                    ThrowEndOfData();
            }

            Buffer = bits << count;
            Count = bitCount - count;

            return (uint)(bits >> (64 - count));
        }
    }

    // Per-block tables, kept per thread so each call doesn't allocate them.
    private sealed class DecoderTables
    {
        [ThreadStatic]
        private static DecoderTables? _current;

        public byte[] Selectors { get; } = new byte[MaxSelectors];

        public byte[] Lengths { get; } = new byte[MaxGroups * MaxAlphaSize];

        // Entries are (symbol << 5) | code length, or 0 for codes longer than LookupBits.
        public ushort[] Lookup { get; } = new ushort[MaxGroups << LookupBits];

        public int[] Limit { get; } = new int[MaxGroups * (MaxCodeLength + 1)];

        public int[] Base { get; } = new int[MaxGroups * (MaxCodeLength + 1)];

        public int[] Perm { get; } = new int[MaxGroups * MaxAlphaSize];

        public int[] MaxLengths { get; } = new int[MaxGroups];

        public int[] ByteCounts { get; } = new int[256];

        public int AlphaSize { get; set; }

        public static DecoderTables Get() => _current ??= new DecoderTables();
    }
}

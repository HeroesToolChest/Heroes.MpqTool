// BZip2InputStream.cs
// Original https://github.com/DinoChiesa/DotNetZip/blob/master/BZip2/BZip2InputStream.cs

using System;
using System.Reflection;

namespace Heroes.MpqTool.BZip2;

/// <summary>
/// A read-only decorator stream that performs BZip2 decompression on Read.
/// </summary>
internal class BZip2InputStream : Stream
{
    private readonly CRC32 _crc = new(true);
    private readonly bool _leaveOpen;

    private bool _disposed;
    private long _totalBytesRead;
    private int _last;

    /* for undoing the Burrows-Wheeler transform */
    private int _origPtr;

    // blockSize100k: 0 .. 9.
    //
    // This var name is a misnomer. The actual block size is 100000
    // * blockSize100k. (not 100k * blocksize100k)
    private int _blockSize100k;
    private bool _blockRandomised;
    private int _bsBuff;
    private int _bsLive;

    private int _nInUse;
    private Stream _input;
    private int _currentChar = -1;

    private uint _storedBlockCRC;
    private uint _storedCombinedCRC;
    private uint _computedBlockCRC;
    private uint _computedCombinedCRC;

    // Variables used by setup* methods exclusively
    private int _suCount;
    private int _suCh2;
    private int _suChPrev;
    private int _suI2;
    private int _suJ2;
    private int _suRNToGo;
    private int _suRTPos;
    private int _suTPos;
    private char _suZ;

    private DecompressionState? _data;

    private CState _currentState = CState.START_BLOCK;

    /// <summary>
    /// Initializes a new instance of the <see cref="BZip2InputStream"/> class, wrapping it around the given input Stream.
    /// </summary>
    /// <remarks>
    /// The input stream will be closed when the BZip2InputStream is closed.
    /// </remarks>
    /// <param name='input'>The stream from which to read compressed data.</param>
    public BZip2InputStream(Stream input)
        : this(input, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BZip2InputStream"/> class and
    /// specifying whether to leave the wrapped stream open when
    /// the BZip2InputStream is closed.
    /// </summary>
    /// <param name='input'>The stream from which to read compressed data.</param>
    /// <param name='leaveOpen'>
    ///  Whether to leave the input stream open, when the BZip2InputStream closes.
    /// </param>
    public BZip2InputStream(Stream input, bool leaveOpen)
        : base()
    {
        _input = input;
        _leaveOpen = leaveOpen;
        Init();
    }

    /// <summary>
    /// Compressor State.
    /// </summary>
    private enum CState
    {
        EOF = 0,
        START_BLOCK = 1,
        RAND_PART_A = 2,
        RAND_PART_B = 3,
        RAND_PART_C = 4,
        NO_RAND_PART_A = 5,
        NO_RAND_PART_B = 6,
        NO_RAND_PART_C = 7,
    }

    /// <summary>
    /// Gets a value indicating whether the stream can be read.
    /// </summary>
    /// <remarks>
    /// The return value depends on whether the captive stream supports reading.
    /// </remarks>
    public override bool CanRead
    {
        get
        {
#if NET8_0_OR_GREATER
            ObjectDisposedException.ThrowIf(_disposed, "BZip2Stream");
#else
            if (_disposed) throw new ObjectDisposedException("BZip2Stream");
#endif
            return _input.CanRead;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the stream supports Seek operations.
    /// </summary>
    /// <remarks>
    /// Always returns false.
    /// </remarks>
    public override bool CanSeek
    {
        get { return false; }
    }

    /// <summary>
    /// Gets a value indicating whether the stream can be written.
    /// </summary>
    /// <remarks>
    /// The return value depends on whether the captive stream supports writing.
    /// </remarks>
    public override bool CanWrite
    {
        get
        {
#if NET8_0_OR_GREATER
            ObjectDisposedException.ThrowIf(_disposed, "BZip2Stream");
#else
            if (_disposed) throw new ObjectDisposedException("BZip2Stream");
#endif
            return _input.CanWrite;
        }
    }

    public override long Length
    {
        get { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the position of the stream pointer.
    /// </summary>
    ///
    /// <remarks>
    /// Setting this property always throws a <see
    /// cref="NotImplementedException"/>. Reading will return the
    /// total number of uncompressed bytes read in.
    /// </remarks>
    public override long Position
    {
        get
        {
            return _totalBytesRead;
        }

        set
        {
            throw new NotImplementedException();
        }
    }

#if NET8_0_OR_GREATER
    private static ReadOnlySpan<int> RNums =>
    [
#else
    private static ReadOnlySpan<int> RNums => new[]
    {
#endif
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
#if NET8_0_OR_GREATER
    ];
#else
    };
#endif

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (offset < 0)
            throw new IndexOutOfRangeException(string.Format("offset ({0}) must be > 0", offset));

        if (count < 0)
            throw new IndexOutOfRangeException(string.Format("count ({0}) must be > 0", count));

        if (offset + count > buffer.Length)
            throw new IndexOutOfRangeException(string.Format("offset({0}) count({1}) bLength({2})", offset, count, buffer.Length));

        if (_input is null)
            throw new IOException("the stream is not open");

        int hi = offset + count;
        int destOffset = offset;

        for (int b; (destOffset < hi) && ((b = ReadByte()) >= 0);)
        {
            buffer[destOffset++] = (byte)b;
        }

        return (destOffset == offset) ? -1 : (destOffset - offset);
    }

    /// <summary>
    /// Read a single byte from the stream.
    /// </summary>
    /// <returns>the byte read from the stream, or -1 if EOF.</returns>
    public override int ReadByte()
    {
        int retChar = _currentChar;
        _totalBytesRead++;

        switch (_currentState)
        {
            case CState.EOF:
                return -1;

            case CState.START_BLOCK:
                throw new IOException("bad state");

            case CState.RAND_PART_A:
                throw new IOException("bad state");

            case CState.RAND_PART_B:
                SetupRandPartB();
                break;

            case CState.RAND_PART_C:
                SetupRandPartC();
                break;

            case CState.NO_RAND_PART_A:
                throw new IOException("bad state");

            case CState.NO_RAND_PART_B:
                SetupNoRandPartB();
                break;

            case CState.NO_RAND_PART_C:
                SetupNoRandPartC();
                break;

            default:
                throw new IOException("bad state");
        }

        return retChar;
    }

    /// <summary>
    /// Flush the stream.
    /// </summary>
    public override void Flush()
    {
#if NET8_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed, "BZip2Stream");
#else
        if (_disposed) throw new ObjectDisposedException("BZip2Stream");
#endif
        _input.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///   Close the stream.
    /// </summary>
    public override void Close()
    {
        Stream inShadow = _input;
        if (inShadow != null)
        {
            try
            {
                if (!_leaveOpen)
                    inShadow.Close();
            }
            finally
            {
                _data = null;
                _input = null!;
            }
        }
    }

    /// <summary>
    ///  Dispose the stream.
    /// </summary>
    /// <param name="disposing">
    ///  Indicates whether the Dispose method was invoked by user code.
    /// </param>
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (!_disposed)
            {
                if (disposing && (_input != null))
                    _input.Close();

                _disposed = true;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static void HbCreateDecodeTables(int[] limit, List<int> bbase, List<int> perm, char[] length, int minLen, int maxLen, int alphaSize)
    {
        for (int i = minLen; i <= maxLen; i++)
        {
            for (int j = 0; j < alphaSize; j++)
            {
                if (length[j] == i)
                {
                    perm.Add(j);
                }
            }
        }

        for (int i = 0; i < alphaSize; i++)
        {
            int index = length[i] + 1;

            if (index >= bbase.Count)
            {
                for (int j = bbase.Count; j <= index; j++)
                {
                    bbase.Add(0);
                }
            }

            bbase[length[i] + 1]++;
        }

        int fill = BZip2.MaxCodeLength - bbase.Count;
        if (fill > 0)
            bbase.AddRange(Enumerable.Repeat(0, fill));

        for (int i = 1, b = bbase[0]; i < BZip2.MaxCodeLength; i++)
        {
            b += bbase[i];
            bbase[i] = b;
        }

        for (int i = minLen, vec = 0, b = bbase[i]; i <= maxLen; i++)
        {
            int nb = bbase[i + 1];
            vec += nb - b;
            b = nb;
            limit[i] = vec - 1;
            vec <<= 1;
        }

        for (int i = minLen + 1; i <= maxLen; i++)
        {
            bbase[i] = ((limit[i - 1] + 1) << 1) - bbase[i];
        }
    }

    private void MakeMaps(Span<bool> inUseSpan)
    {
        byte[] seqToUnseq = _data!.SeqToUnseq;

        int n = 0;

        for (int i = 0; i < 256; i++)
        {
            if (inUseSpan[i])
                seqToUnseq[n++] = (byte)i;
        }

        _nInUse = n;
    }

    private void Init()
    {
        if (_input is null)
            throw new IOException("No input Stream");

        if (!_input.CanRead)
            throw new IOException("Unreadable input Stream");

        CheckMagicChar('B', 0);
        CheckMagicChar('Z', 1);
        CheckMagicChar('h', 2);

        int blockSize = _input.ReadByte();

        if ((blockSize < '1') || (blockSize > '9'))
        {
            throw new IOException($"Stream is not BZip2 formatted: illegal blocksize {(char)blockSize}");
        }

        _blockSize100k = blockSize - '0';

        InitBlock();
        SetupBlock();
    }

    private void CheckMagicChar(char expected, int position)
    {
        int magic = _input.ReadByte();
        if (magic != expected)
        {
            throw new IOException($"Not a valid BZip2 stream. byte {position}, expected '{expected}', got '{magic}'");
        }
    }

    private void InitBlock()
    {
        char magic0 = BsGetUByte();
        char magic1 = BsGetUByte();
        char magic2 = BsGetUByte();
        char magic3 = BsGetUByte();
        char magic4 = BsGetUByte();
        char magic5 = BsGetUByte();

        if (magic0 == 0x17 && magic1 == 0x72 && magic2 == 0x45 && magic3 == 0x38 && magic4 == 0x50 && magic5 == 0x90)
        {
            Complete(); // end of file
        }
        else if (magic0 != 0x31 ||
                 magic1 != 0x41 ||
                 magic2 != 0x59 ||
                 magic3 != 0x26 ||
                 magic4 != 0x53 ||
                 magic5 != 0x59)
        {
            _currentState = CState.EOF;

            throw new IOException(string.Format("bad block header at offset 0x{0:X}", _input.Position));
        }
        else
        {
            _storedBlockCRC = BsGetInt();
            _blockRandomised = GetBits(1) == 1;

            // Lazily allocate data
            _data ??= new DecompressionState(_blockSize100k);

            // currBlockNo++;
            GetAndMoveToFrontDecode();

            _crc.Reset();
            _currentState = CState.START_BLOCK;
        }
    }

    private void EndBlock()
    {
        _computedBlockCRC = (uint)_crc.Crc32Result;

        // A bad CRC is considered a fatal error.
        if (_storedBlockCRC != _computedBlockCRC)
        {
            throw new IOException(string.Format("BZip2 CRC error (expected {0:X8}, computed {1:X8})", _storedBlockCRC, _computedBlockCRC));
        }

        _computedCombinedCRC = (_computedCombinedCRC << 1) | (_computedCombinedCRC >> 31);
        _computedCombinedCRC ^= _computedBlockCRC;
    }

    private void Complete()
    {
        _storedCombinedCRC = BsGetInt();
        _currentState = CState.EOF;

        _data = null;

        if (_storedCombinedCRC != _computedCombinedCRC)
        {
            throw new IOException(string.Format("BZip2 CRC error (expected {0:X8}, computed {1:X8})", _storedCombinedCRC, _computedCombinedCRC));
        }
    }

    /// <summary>
    /// Read n bits from input, right justifying the result.
    /// </summary>
    /// <remarks>
    ///  For example, if you read 1 bit, the result is either 0 or 1.
    /// </remarks>
    /// <param name ="n">
    /// The number of bits to read, always between 1 and 32.
    /// </param>
    private int GetBits(int n)
    {
        int bsLiveShadow = _bsLive;
        int bsBuffShadow = _bsBuff;

        if (bsLiveShadow < n)
        {
            do
            {
                int thech = _input.ReadByte();

                if (thech < 0)
                    throw new IOException("unexpected end of stream");

                bsBuffShadow = (bsBuffShadow << 8) | thech;
                bsLiveShadow += 8;
            }
            while (bsLiveShadow < n);

            _bsBuff = bsBuffShadow;
        }

        _bsLive = bsLiveShadow - n;
        return (bsBuffShadow >> (bsLiveShadow - n)) & ((1 << n) - 1);
    }

    private bool BsGetBit()
    {
        int bit = GetBits(1);
        return bit != 0;
    }

    private char BsGetUByte()
    {
        return (char)GetBits(8);
    }

    private uint BsGetInt()
    {
        return (uint)((((((GetBits(8) << 8) | GetBits(8)) << 8) | GetBits(8)) << 8) | GetBits(8));
    }

    private void RecvDecodingTables()
    {
        DecompressionState s = _data!;
        byte[] pos = s.RecvDecodingTables_pos;

        int inUse16 = 0;

        /* Receive the mapping table */
        for (int i = 0; i < 16; i++)
        {
            if (BsGetBit())
            {
                inUse16 |= 1 << i;
            }
        }

        Span<bool> inUseSpan = stackalloc bool[256];

        for (int i = 256; --i >= 0;)
        {
            inUseSpan[i] = false;
        }

        for (int i = 0; i < 16; i++)
        {
            if ((inUse16 & (1 << i)) != 0)
            {
                int i16 = i << 4;
                for (int j = 0; j < 16; j++)
                {
                    if (BsGetBit())
                    {
                        inUseSpan[i16 + j] = true;
                    }
                }
            }
        }

        MakeMaps(inUseSpan);
        int alphaSize = _nInUse + 2;

        /* Now the selectors */
        int nGroups = GetBits(3);
        int nSelectors = GetBits(15);

        SelectorMtf(s, pos, nGroups, nSelectors);

        char[][] len = s.TempCharArray2d;

        /* Now the coding tables */
        for (int t = 0; t < nGroups; t++)
        {
            int curr = GetBits(5);
            char[] len_t = len[t];
            for (int i = 0; i < alphaSize; i++)
            {
                while (BsGetBit())
                {
                    curr += BsGetBit() ? -1 : 1;
                }

                len_t[i] = (char)curr;
            }
        }

        // finally create the Huffman tables
        CreateHuffmanDecodingTables(alphaSize, nGroups);
    }

    private void SelectorMtf(DecompressionState s, byte[] pos, int nGroups, int nSelectors)
    {
        Span<byte> selectorMtfSpan = stackalloc byte[nSelectors];

        for (int i = 0; i < nSelectors; i++)
        {
            int j = 0;
            while (BsGetBit())
            {
                j++;
            }

            selectorMtfSpan[i] = (byte)j;
        }

        /* Undo the MTF values for the selectors. */
        for (int v = nGroups; --v >= 0;)
        {
            pos[v] = (byte)v;
        }

        for (int i = 0; i < nSelectors; i++)
        {
            int v = selectorMtfSpan[i];
            byte tmp = pos[v];
            while (v > 0)
            {
                // nearly all times v is zero, 4 in most other cases
                pos[v] = pos[v - 1];
                v--;
            }

            pos[0] = tmp;
            s.SelectorList.Add(tmp);
        }
    }

    /**
     * Called by recvDecodingTables() exclusively.
     */
    private void CreateHuffmanDecodingTables(int alphaSize, int nGroups)
    {
        DecompressionState s = _data!;
        char[][] len = s.TempCharArray2d;

        for (int t = 0; t < nGroups; t++)
        {
            int minLen = 32;
            int maxLen = 0;
            char[] len_t = len[t];
            for (int i = alphaSize; --i >= 0;)
            {
                char lent = len_t[i];
                if (lent > maxLen)
                    maxLen = lent;

                if (lent < minLen)
                    minLen = lent;
            }

            s.GLimitList.Add(new int[Math.Max(maxLen, BZip2.MaxCodeLength)]);
            s.GBaseList.Add(new List<int>());
            s.GPermList.Add(new List<int>());

            HbCreateDecodeTables(s.GLimitList[t], s.GBaseList[t], s.GPermList[t], len[t], minLen, maxLen, alphaSize);
            s.GMinlen[t] = minLen;
        }
    }

    private void GetAndMoveToFrontDecode()
    {
        DecompressionState s = _data!;
        _origPtr = GetBits(24);

        if (_origPtr < 0)
            throw new IOException("BZ_DATA_ERROR");
        if (_origPtr > 10 + (BZip2.BlockSizeMultiple * _blockSize100k))
            throw new IOException("BZ_DATA_ERROR");

        RecvDecodingTables();

        byte[] yy = s.GetAndMoveToFrontDecode_yy;
        int limitLast = _blockSize100k * BZip2.BlockSizeMultiple;

        /*
         * Setting up the unzftab entries here is not strictly necessary, but it
         * does save having to do it later in a separate pass, and so saves a
         * block's worth of cache misses.
         */
        for (int i = 256; --i >= 0;)
        {
            yy[i] = (byte)i;
            s.Unzftab[i] = 0;
        }

        int groupNo = 0;
        int groupPos = BZip2.GSize - 1;
        int eob = _nInUse + 1;
        int nextSym = GetAndMoveToFrontDecode0(0);
        int bsBuffShadow = _bsBuff;
        int bsLiveShadow = _bsLive;
        int lastShadow = -1;
        int zt = s.SelectorList[groupNo] & 0xff;
        List<int> base_zt = s.GBaseList[zt];
        int[] limit_zt = s.GLimitList[zt];
        List<int> perm_zt = s.GPermList[zt];
        int minLens_zt = s.GMinlen[zt];

        while (nextSym != eob)
        {
            if ((nextSym == BZip2.RUNA) || (nextSym == BZip2.RUNB))
            {
                int es = -1;

                for (int n = 1; true; n <<= 1)
                {
                    if (nextSym == BZip2.RUNA)
                    {
                        es += n;
                    }
                    else if (nextSym == BZip2.RUNB)
                    {
                        es += n << 1;
                    }
                    else
                    {
                        break;
                    }

                    if (groupPos == 0)
                    {
                        groupPos = BZip2.GSize - 1;

                        if (++groupNo >= s.SelectorList.Count)
                        {
                            for (int j = s.SelectorList.Count; j <= groupNo; j++)
                            {
                                s.SelectorList.Add(0);
                            }
                        }

                        zt = s.SelectorList[groupNo] & 0xff;
                        base_zt = s.GBaseList[zt];
                        limit_zt = s.GLimitList[zt];
                        perm_zt = s.GPermList[zt];
                        minLens_zt = s.GMinlen[zt];
                    }
                    else
                    {
                        groupPos--;
                    }

                    int zn = minLens_zt;

                    // Inlined:
                    // int zvec = GetBits(zn);
                    while (bsLiveShadow < zn)
                    {
                        int thech = _input.ReadByte();
                        if (thech >= 0)
                        {
                            bsBuffShadow = (bsBuffShadow << 8) | thech;
                            bsLiveShadow += 8;
                            continue;
                        }
                        else
                        {
                            throw new IOException("unexpected end of stream");
                        }
                    }

                    int zvec = (bsBuffShadow >> (bsLiveShadow - zn)) & ((1 << zn) - 1);
                    bsLiveShadow -= zn;

                    while (zvec > limit_zt[zn])
                    {
                        zn++;
                        while (bsLiveShadow < 1)
                        {
                            int thech = _input.ReadByte();
                            if (thech >= 0)
                            {
                                bsBuffShadow = (bsBuffShadow << 8) | thech;
                                bsLiveShadow += 8;
                                continue;
                            }
                            else
                            {
                                throw new IOException("unexpected end of stream");
                            }
                        }

                        bsLiveShadow--;
                        zvec = (zvec << 1) | ((bsBuffShadow >> bsLiveShadow) & 1);
                    }

                    nextSym = perm_zt[zvec - base_zt[zn]];
                }

                byte ch = s.SeqToUnseq[yy[0]];
                s.Unzftab[ch & 0xff] += es + 1;

                while (es-- >= 0)
                {
                    ++lastShadow;
                    s.Ll8List.Add(ch);
                }

                if (lastShadow >= limitLast)
                    throw new IOException("block overrun");
            }
            else
            {
                if (++lastShadow >= limitLast)
                    throw new IOException("block overrun");

                byte tmp = yy[nextSym - 1];
                s.Unzftab[s.SeqToUnseq[tmp] & 0xff]++;
                s.Ll8List.Add(s.SeqToUnseq[tmp]);

                /*
                 * This loop is hammered during decompression, hence avoid
                 * native method call overhead of System.Buffer.BlockCopy for very
                 * small ranges to copy.
                 */
                if (nextSym <= 16)
                {
                    for (int j = nextSym - 1; j > 0;)
                    {
                        yy[j] = yy[--j];
                    }
                }
                else
                {
                    Buffer.BlockCopy(yy, 0, yy, 1, nextSym - 1);
                }

                yy[0] = tmp;

                if (groupPos == 0)
                {
                    groupPos = BZip2.GSize - 1;

                    if (++groupNo >= s.SelectorList.Count)
                    {
                        for (int j = s.SelectorList.Count; j <= groupNo; j++)
                        {
                            s.SelectorList.Add(0);
                        }
                    }

                    zt = s.SelectorList[groupNo] & 0xff;
                    base_zt = s.GBaseList[zt];
                    limit_zt = s.GLimitList[zt];
                    perm_zt = s.GPermList[zt];
                    minLens_zt = s.GMinlen[zt];
                }
                else
                {
                    groupPos--;
                }

                int zn = minLens_zt;

                // Inlined:
                // int zvec = GetBits(zn);
                while (bsLiveShadow < zn)
                {
                    int thech = _input.ReadByte();
                    if (thech >= 0)
                    {
                        bsBuffShadow = (bsBuffShadow << 8) | thech;
                        bsLiveShadow += 8;
                        continue;
                    }
                    else
                    {
                        throw new IOException("unexpected end of stream");
                    }
                }

                int zvec = (bsBuffShadow >> (bsLiveShadow - zn))
                    & ((1 << zn) - 1);
                bsLiveShadow -= zn;

                while (zvec > limit_zt[zn])
                {
                    zn++;
                    while (bsLiveShadow < 1)
                    {
                        int thech = _input.ReadByte();
                        if (thech >= 0)
                        {
                            bsBuffShadow = (bsBuffShadow << 8) | thech;
                            bsLiveShadow += 8;
                            continue;
                        }
                        else
                        {
                            throw new IOException("unexpected end of stream");
                        }
                    }

                    bsLiveShadow--;
                    zvec = (zvec << 1) | ((bsBuffShadow >> bsLiveShadow) & 1);
                }

                nextSym = perm_zt[zvec - base_zt[zn]];
            }
        }

        _last = lastShadow;
        _bsLive = bsLiveShadow;
        _bsBuff = bsBuffShadow;
    }

    private int GetAndMoveToFrontDecode0(int groupNo)
    {
        DecompressionState s = _data!;
        int zt = s.SelectorList[groupNo] & 0xff;
        int[] limit_zt = s.GLimitList[zt];
        int zn = s.GMinlen[zt];
        int zvec = GetBits(zn);
        int bsLiveShadow = _bsLive;
        int bsBuffShadow = _bsBuff;

        while (zvec > limit_zt[zn])
        {
            zn++;
            while (bsLiveShadow < 1)
            {
                int thech = _input.ReadByte();

                if (thech >= 0)
                {
                    bsBuffShadow = (bsBuffShadow << 8) | thech;
                    bsLiveShadow += 8;
                    continue;
                }
                else
                {
                    throw new IOException("unexpected end of stream");
                }
            }

            bsLiveShadow--;
            zvec = (zvec << 1) | ((bsBuffShadow >> bsLiveShadow) & 1);
        }

        _bsLive = bsLiveShadow;
        _bsBuff = bsBuffShadow;

        return s.GPermList[zt][zvec - s.GBaseList[zt][zn]];
    }

    private void SetupBlock()
    {
        if (_data is null)
            return;

        int i;
        DecompressionState s = _data;
        int[] tt = s.InitTT(_last + 1);

        /* Check: unzftab entries in range. */
        for (i = 0; i <= 255; i++)
        {
            if (s.Unzftab[i] < 0 || s.Unzftab[i] > _last)
                throw new Exception("BZ_DATA_ERROR");
        }

        /* Actually generate cftab. */
        Span<int> cftabSpan = stackalloc int[257];

        cftabSpan[0] = 0;
        for (i = 1; i <= 256; i++) cftabSpan[i] = s.Unzftab[i - 1];
        for (i = 1; i <= 256; i++) cftabSpan[i] += cftabSpan[i - 1];

        /* Check: cftab entries in range. */
        for (i = 0; i <= 256; i++)
        {
            if (cftabSpan[i] < 0 || cftabSpan[i] > _last + 1)
            {
                throw new Exception(string.Format("BZ_DATA_ERROR: cftab[{0}]={1} last={2}", i, cftabSpan[i], _last));
            }
        }

        /* Check: cftab entries non-descending. */
        for (i = 1; i <= 256; i++)
        {
            if (cftabSpan[i - 1] > cftabSpan[i])
                throw new Exception("BZ_DATA_ERROR");
        }

        int lastShadow;
        for (i = 0, lastShadow = _last; i <= lastShadow; i++)
        {
            tt[cftabSpan[s.Ll8List[i] & 0xff]++] = i;
        }

        if ((_origPtr < 0) || (_origPtr >= tt.Length))
            throw new IOException("stream corrupted");

        _suTPos = tt[_origPtr];
        _suCount = 0;
        _suI2 = 0;
        _suCh2 = 256; /* not a valid 8-bit byte value?, and not EOF */

        if (_blockRandomised)
        {
            _suRNToGo = 0;
            _suRTPos = 0;
            SetupRandPartA();
        }
        else
        {
            SetupNoRandPartA();
        }
    }

    private void SetupRandPartA()
    {
        if (_suI2 <= _last)
        {
            _suChPrev = _suCh2;
            int su_ch2Shadow = _data!.Ll8List[_suTPos] & 0xff;
            _suTPos = _data.Tt![_suTPos];
            if (_suRNToGo == 0)
            {
                _suRNToGo = RNums[_suRTPos] - 1;
                if (++_suRTPos == 512)
                {
                    _suRTPos = 0;
                }
            }
            else
            {
                _suRNToGo--;
            }

            _suCh2 = su_ch2Shadow ^= (_suRNToGo == 1) ? 1 : 0;
            _suI2++;
            _currentChar = su_ch2Shadow;
            _currentState = CState.RAND_PART_B;
            _crc.UpdateCRC((byte)su_ch2Shadow);
        }
        else
        {
            EndBlock();
            InitBlock();
            SetupBlock();
        }
    }

    private void SetupNoRandPartA()
    {
        if (_suI2 <= _last)
        {
            _suChPrev = _suCh2;
            int su_ch2Shadow = _data!.Ll8List[_suTPos] & 0xff;
            _suCh2 = su_ch2Shadow;
            _suTPos = _data.Tt![_suTPos];
            _suI2++;
            _currentChar = su_ch2Shadow;
            _currentState = CState.NO_RAND_PART_B;
            _crc.UpdateCRC((byte)su_ch2Shadow);
        }
        else
        {
            _currentState = CState.NO_RAND_PART_A;

            EndBlock();
            InitBlock();
            SetupBlock();
        }
    }

    private void SetupRandPartB()
    {
        if (_suCh2 != _suChPrev)
        {
            _currentState = CState.RAND_PART_A;
            _suCount = 1;
            SetupRandPartA();
        }
        else if (++_suCount >= 4)
        {
            _suZ = (char)(_data!.Ll8List[_suTPos] & 0xff);
            _suTPos = _data.Tt![_suTPos];
            if (_suRNToGo == 0)
            {
                _suRNToGo = RNums[_suRTPos] - 1;
                if (++_suRTPos == 512)
                {
                    _suRTPos = 0;
                }
            }
            else
            {
                _suRNToGo--;
            }

            _suJ2 = 0;
            _currentState = CState.RAND_PART_C;
            if (_suRNToGo == 1)
            {
                _suZ ^= (char)1;
            }

            SetupRandPartC();
        }
        else
        {
            _currentState = CState.RAND_PART_A;
            SetupRandPartA();
        }
    }

    private void SetupRandPartC()
    {
        if (_suJ2 < _suZ)
        {
            _currentChar = _suCh2;
            _crc.UpdateCRC((byte)_suCh2);
            _suJ2++;
        }
        else
        {
            _currentState = CState.RAND_PART_A;
            _suI2++;
            _suCount = 0;
            SetupRandPartA();
        }
    }

    private void SetupNoRandPartB()
    {
        if (_suCh2 != _suChPrev)
        {
            _suCount = 1;
            SetupNoRandPartA();
        }
        else if (++_suCount >= 4)
        {
            _suZ = (char)(_data!.Ll8List[_suTPos] & 0xff);
            _suTPos = _data.Tt![_suTPos];
            _suJ2 = 0;
            SetupNoRandPartC();
        }
        else
        {
            SetupNoRandPartA();
        }
    }

    private void SetupNoRandPartC()
    {
        if (_suJ2 < _suZ)
        {
            int su_ch2Shadow = _suCh2;
            _currentChar = su_ch2Shadow;
            _crc.UpdateCRC((byte)su_ch2Shadow);
            _suJ2++;
            _currentState = CState.NO_RAND_PART_C;
        }
        else
        {
            _suI2++;
            _suCount = 0;
            SetupNoRandPartA();
        }
    }

    private sealed class DecompressionState
    {
        public DecompressionState(int blockSize100k)
        {
            Unzftab = new int[256]; // 1024 byte

            GLimitList = new();
            GBaseList = new();
            GPermList = new();
            GMinlen = new int[BZip2.NGroups]; // 24 byte

            GetAndMoveToFrontDecode_yy = new byte[256]; // 512 byte
            TempCharArray2d = BZip2.InitRectangularArray<char>(BZip2.NGroups, BZip2.MaxAlphaSize);
            RecvDecodingTables_pos = new byte[BZip2.NGroups]; // 6 byte

            Ll8List = new();
        }

        public byte[] SeqToUnseq { get; } = new byte[256]; // 256 byte

        public List<byte> SelectorList { get; } = new();

        /**
         * Freq table collected to save a pass over the data during
         * decompression.
         */
        public int[] Unzftab { get; }

        public List<int[]> GLimitList { get; }

        public List<List<int>> GBaseList { get; }

        public List<List<int>> GPermList { get; }

        public int[] GMinlen { get; }

        public byte[] GetAndMoveToFrontDecode_yy { get; }

        public char[][] TempCharArray2d { get; }

        public byte[] RecvDecodingTables_pos { get; }

        public int[]? Tt { get; private set; }

        public List<byte> Ll8List { get; }

        /**
         * Initializes the tt array.
         *
         * This method is called when the required length of the array is known.
         * I don't initialize it at construction time to avoid unneccessary
         * memory allocation when compressing small files.
         */
        public int[] InitTT(int length)
        {
            int[]? ttShadow = Tt;

            // tt.length should always be >= length, but theoretically
            // it can happen, if the compressor mixed small and large
            // blocks. Normally only the last block will be smaller
            // than others.
            if ((ttShadow is null) || (ttShadow.Length < length))
            {
                Tt = ttShadow = new int[length];
            }

            return ttShadow;
        }
    }
}
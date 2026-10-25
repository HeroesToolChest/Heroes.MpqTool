using System.Text;

namespace Heroes.MpqTool.Tests;

[TestClass]
public class BitReaderTests
{
    private static readonly byte[] _mixedBytes = [8, 130, 67, 58, 92, 80, 114, 102, 0, 1, 12, 13, 115, 23, 28, 56, 47, 35];

    [TestMethod]
    public void Constructor_ForBuffer_SetsProperties()
    {
        // act
        BitReader bitReader = new([1, 2, 3, 4], EndianType.LittleEndian);

        // assert
        Assert.AreEqual(4, bitReader.Length);
        Assert.AreEqual(0, bitReader.Index);
        Assert.AreEqual(EndianType.LittleEndian, bitReader.EndianType);
    }

    [TestMethod]
    public void Index_Set_MovesWhereAlignedReadsStart()
    {
        // arrange
        BitReader bitReader = new([1, 2, 3, 4], EndianType.BigEndian);

        // act
        bitReader.Index = 2;

        // assert
        Assert.AreEqual(3, bitReader.ReadAlignedByte());
        Assert.AreEqual(3, bitReader.Index);
    }

    [TestMethod]
    public void EndianType_Set_ChangesByteOrderOfLaterReads()
    {
        // arrange
        BitReader bitReader = new([0x12, 0x34, 0x12, 0x34], EndianType.BigEndian);

        // act
        ushort bigEndian = bitReader.ReadUInt16Aligned();
        bitReader.EndianType = EndianType.LittleEndian;
        ushort littleEndian = bitReader.ReadUInt16Aligned();

        // assert
        Assert.AreEqual((ushort)0x1234, bigEndian);
        Assert.AreEqual((ushort)0x3412, littleEndian);
    }

    [TestMethod]
    [DataRow(new byte[] { 0, (byte)'c', (byte)'b', (byte)'a', 0 }, "abc")]
    [DataRow(new byte[] { 0, 0, (byte)'a', 0 }, "a")]
    [DataRow(new byte[] { 0, 0, 0, 0 }, "")]
    public void GetUTF8StringFromBytes_ForBytes_TrimsZerosAndReverses(byte[] bytes, string expected)
    {
        // the bytes are reversed only on little endian machines, to undo BitConverter writing them in little endian order
        if (!BitConverter.IsLittleEndian)
            Assert.Inconclusive("This test is for little endian machines.");

        // act
        string result = BitReader.GetUTF8StringFromBytes(bytes);

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ReadBitsTest()
    {
        Span<byte> buffer = [8, 130, 67, 58];

        BitReader bitReader = new(buffer, EndianType.BigEndian);

        uint result = bitReader.ReadBits(6);

        Assert.AreEqual(8U, result);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xAC, 0x03 }, new int[] { 4, 4, 8 }, new uint[] { 0xC, 0xA, 0x03 })] // the low bits of a byte are read first
    [DataRow(new byte[] { 0xAC, 0x03 }, new int[] { 3, 10 }, new uint[] { 0b100, 675 })] // 5 bits of the first byte, then 5 of the second
    [DataRow(new byte[] { 0x12, 0x34, 0x56 }, new int[] { 24 }, new uint[] { 0x123456 })]
    [DataRow(new byte[] { 0x12, 0x34 }, new int[] { 16 }, new uint[] { 0x1234 })]
    [DataRow(new byte[] { 0x12, 0x34 }, new int[] { 8, 8 }, new uint[] { 0x12, 0x34 })]
    public void ReadBits_ForBigEndian_ReturnsBits(byte[] bytes, int[] bitCounts, uint[] expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);
        uint[] results = new uint[bitCounts.Length];

        // act
        for (int i = 0; i < bitCounts.Length; i++)
            results[i] = bitReader.ReadBits(bitCounts[i]);

        // assert
        CollectionAssert.AreEqual(expected, results);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x12, 0x34 }, 16, 0x3412u)]
    [DataRow(new byte[] { 8, 130, 67, 58 }, 32, 0x3A438208u)]
    [DataRow(new byte[] { 0x12 }, 8, 0x12u)]
    public void ReadBits_ForLittleEndianWholeBytes_ReturnsLittleEndianValue(byte[] bytes, int numberOfBits, uint expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.LittleEndian);

        // act
        uint result = bitReader.ReadBits(numberOfBits);

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ReadBits_ZeroBits_ReturnsZeroAndReadsNothing()
    {
        // arrange
        BitReader bitReader = new([0xAC, 0x03], EndianType.BigEndian);

        // act
        uint result = bitReader.ReadBits(0);

        // assert
        Assert.AreEqual(0u, result);
        Assert.AreEqual(0, bitReader.Index);
        Assert.AreEqual(0xACu, bitReader.ReadBits(8));
    }

    [TestMethod]
    [DataRow(33)]
    [DataRow(-1)]
    public void ReadBits_InvalidNumberOfBits_ThrowsException(int numberOfBits)
    {
        // act / assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BitReader(new byte[8], EndianType.BigEndian).ReadBits(numberOfBits));
    }

    [TestMethod]
    public void ReadsBitsCompareBigEndianTest()
    {
        Span<byte> buffer = [8, 130, 67, 58];
        BitReader bitReader = new(buffer, EndianType.BigEndian);
        int resultBig = bitReader.ReadInt32Aligned();

        BitReader bitReader2 = new(buffer, EndianType.BigEndian);
        int resultBig2 = (int)bitReader2.ReadBits(32);

        Assert.AreEqual(resultBig, resultBig2);
        Assert.AreEqual(0x0882433A, resultBig);
    }

    [TestMethod]
    public void ReadsBitsCompareLittleEndianTest()
    {
        Span<byte> buffer = [8, 130, 67, 58];
        BitReader bitReader = new(buffer, EndianType.LittleEndian);
        uint resultLittle = (uint)bitReader.ReadInt32Aligned();

        BitReader bitReader2 = new(buffer, EndianType.LittleEndian);
        uint resultLittle2 = bitReader2.ReadBits(32);

        Assert.AreEqual(resultLittle, resultLittle2);
        Assert.AreEqual(0x3A438208u, resultLittle);
    }

    [TestMethod]
    [DataRow(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 64, 0x0102030405060708ul)]
    [DataRow(new byte[] { 1, 2, 3, 4, 5 }, 40, 0x0102030405ul)]
    [DataRow(new byte[] { 0xAC, 0x03 }, 13, 0x1583ul)] // 8 bits, then the low 5 bits of the second byte
    public void ReadULongBits_ForBigEndian_ReturnsBits(byte[] bytes, int numberOfBits, ulong expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);

        // act
        ulong result = bitReader.ReadULongBits(numberOfBits);

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ReadULongBits_ForLittleEndian_ReturnsLittleEndianValue()
    {
        // arrange
        BitReader bitReader = new([1, 2, 3, 4, 5, 6, 7, 8], EndianType.LittleEndian);

        // act
        ulong result = bitReader.ReadULongBits(64);

        // assert
        Assert.AreEqual(0x0807060504030201ul, result);
    }

    [TestMethod]
    public void ReadULongBits_UpTo32BitsAtAnyOffset_MatchesReadBits()
    {
        for (int offset = 0; offset < 16; offset++)
        {
            for (int numberOfBits = 1; numberOfBits <= 32; numberOfBits++)
            {
                // arrange
                BitReader bitReader = new(_mixedBytes, EndianType.BigEndian);
                BitReader bitReader2 = new(_mixedBytes, EndianType.BigEndian);
                bitReader.ReadBits(offset);
                bitReader2.ReadBits(offset);

                // act
                ulong result = bitReader.ReadULongBits(numberOfBits);

                // assert
                Assert.AreEqual(bitReader2.ReadBits(numberOfBits), result, $"offset {offset}, {numberOfBits} bits");
            }
        }
    }

    [TestMethod]
    [DataRow(65)]
    [DataRow(0)]
    public void ReadULongBits_InvalidNumberOfBits_ThrowsException(int numberOfBits)
    {
        // act / assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BitReader(new byte[16], EndianType.BigEndian).ReadULongBits(numberOfBits));
    }

    [TestMethod]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 64, -1L)]
    [DataRow(new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0 }, 64, long.MinValue)]
    [DataRow(new byte[] { 1, 2, 3 }, 24, 0x010203L)]
    [DataRow(new byte[] { 0x12, 0x34 }, 16, 0x1234L)]
    public void ReadLongBits_ForBigEndian_ReturnsBits(byte[] bytes, int numberOfBits, long expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);

        // act
        long result = bitReader.ReadLongBits(numberOfBits);

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ReadLongBits_ForLittleEndian_ReturnsLittleEndianValue()
    {
        // arrange
        BitReader bitReader = new([0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], EndianType.LittleEndian);

        // act
        long result = bitReader.ReadLongBits(64);

        // assert
        Assert.AreEqual(-2L, result);
    }

    [TestMethod]
    [DataRow(65)]
    [DataRow(0)]
    public void ReadLongBits_InvalidNumberOfBits_ThrowsException(int numberOfBits)
    {
        // act / assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BitReader(new byte[16], EndianType.BigEndian).ReadLongBits(numberOfBits));
    }

    [TestMethod]
    public void ReadBoolean_ForByte_ReadsLowBitFirst()
    {
        // arrange
        BitReader bitReader = new([0b0000_0101], EndianType.BigEndian);
        bool[] results = new bool[8];

        // act
        for (int i = 0; i < results.Length; i++)
            results[i] = bitReader.ReadBoolean();

        // assert
        Assert.AreSequenceEqual(new[] { true, false, true, false, false, false, false, false }, results);
    }

    [TestMethod]
    public void ReadBoolean_PastEndOfByte_ReadsNextByte()
    {
        // arrange
        BitReader bitReader = new([0b1000_0000, 0b0000_0001], EndianType.BigEndian);

        for (int i = 0; i < 7; i++)
            bitReader.ReadBoolean();

        // act
        bool lastBitOfFirstByte = bitReader.ReadBoolean();
        bool firstBitOfSecondByte = bitReader.ReadBoolean();

        // assert
        Assert.IsTrue(lastBitOfFirstByte);
        Assert.IsTrue(firstBitOfSecondByte);
    }

    [TestMethod]
    public void ReadBitArray_ForIntNumberOfBits_ReturnsBits()
    {
        // arrange
        BitReader bitReader = new([0b0000_0101, 0b0000_0010], EndianType.BigEndian);

        // act
        bool[] result = bitReader.ReadBitArray(10);

        // assert
        Assert.AreSequenceEqual(new[] { true, false, true, false, false, false, false, false, false, true }, result);
    }

    [TestMethod]
    public void ReadBitArray_ForUIntNumberOfBits_ReturnsBits()
    {
        // arrange
        BitReader bitReader = new([0b0000_0101, 0b0000_0010], EndianType.BigEndian);

        // act
        bool[] result = bitReader.ReadBitArray(10u);

        // assert
        Assert.AreSequenceEqual(new[] { true, false, true, false, false, false, false, false, false, true }, result);
    }

    [TestMethod]
    public void ReadBitArray_ForSpan_FillsSpan()
    {
        // arrange
        BitReader bitReader = new([0b0000_0101, 0b0000_0010], EndianType.BigEndian);
        bool[] destination = new bool[10];

        // act
        bitReader.ReadBitArray(destination);

        // assert
        Assert.AreSequenceEqual(new[] { true, false, true, false, false, false, false, false, false, true }, destination);
    }

    [TestMethod]
    public void ReadBitArray_ForStackallocSpan_FillsSpan()
    {
        // arrange
        // the reader is over an array, so it could outlive the stack memory; this only compiles because the parameter is scoped
        BitReader bitReader = new([0b0000_0101, 0b0000_0010], EndianType.BigEndian);
        Span<bool> destination = stackalloc bool[10];

        // act
        bitReader.ReadBitArray(destination);

        // assert
        Assert.AreSequenceEqual(new[] { true, false, true, false, false, false, false, false, false, true }, destination.ToArray());
    }

    [TestMethod]
    public void ReadBitArray_ZeroBits_ReturnsEmptyArray()
    {
        // arrange
        BitReader bitReader = new([0xFF], EndianType.BigEndian);

        // act
        bool[] result = bitReader.ReadBitArray(0);

        // assert
        Assert.IsEmpty(result);
        Assert.IsTrue(bitReader.ReadBoolean());
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian, (ushort)0x1234)]
    [DataRow(EndianType.LittleEndian, (ushort)0x3412)]
    public void ReadUInt16Aligned_ForEndianType_ReturnsValue(EndianType endianType, ushort expected)
    {
        // arrange
        BitReader bitReader = new([0x12, 0x34], endianType);

        // act
        ushort result = bitReader.ReadUInt16Aligned();

        // assert
        Assert.AreEqual(expected, result);
        Assert.AreEqual(2, bitReader.Index);
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian, new byte[] { 0xFF, 0xFE })]
    [DataRow(EndianType.LittleEndian, new byte[] { 0xFE, 0xFF })]
    public void ReadInt16Aligned_ForEndianType_ReturnsNegativeValue(EndianType endianType, byte[] bytes)
    {
        // arrange
        BitReader bitReader = new(bytes, endianType);

        // act
        short result = bitReader.ReadInt16Aligned();

        // assert
        Assert.AreEqual((short)-2, result);
        Assert.AreEqual(2, bitReader.Index);
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian, 0x12345678u)]
    [DataRow(EndianType.LittleEndian, 0x78563412u)]
    public void ReadUInt32Aligned_ForEndianType_ReturnsValue(EndianType endianType, uint expected)
    {
        // arrange
        BitReader bitReader = new([0x12, 0x34, 0x56, 0x78], endianType);

        // act
        uint result = bitReader.ReadUInt32Aligned();

        // assert
        Assert.AreEqual(expected, result);
        Assert.AreEqual(4, bitReader.Index);
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian, new byte[] { 0xFF, 0xFF, 0xFF, 0xFE })]
    [DataRow(EndianType.LittleEndian, new byte[] { 0xFE, 0xFF, 0xFF, 0xFF })]
    public void ReadInt32Aligned_ForEndianType_ReturnsNegativeValue(EndianType endianType, byte[] bytes)
    {
        // arrange
        BitReader bitReader = new(bytes, endianType);

        // act
        int result = bitReader.ReadInt32Aligned();

        // assert
        Assert.AreEqual(-2, result);
        Assert.AreEqual(4, bitReader.Index);
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian, 0x0102030405060708ul)]
    [DataRow(EndianType.LittleEndian, 0x0807060504030201ul)]
    public void ReadUInt64Aligned_ForEndianType_ReturnsValue(EndianType endianType, ulong expected)
    {
        // arrange
        BitReader bitReader = new([1, 2, 3, 4, 5, 6, 7, 8], endianType);

        // act
        ulong result = bitReader.ReadUInt64Aligned();

        // assert
        Assert.AreEqual(expected, result);
        Assert.AreEqual(8, bitReader.Index);
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE })]
    [DataRow(EndianType.LittleEndian, new byte[] { 0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF })]
    public void ReadInt64Aligned_ForEndianType_ReturnsNegativeValue(EndianType endianType, byte[] bytes)
    {
        // arrange
        BitReader bitReader = new(bytes, endianType);

        // act
        long result = bitReader.ReadInt64Aligned();

        // assert
        Assert.AreEqual(-2L, result);
        Assert.AreEqual(8, bitReader.Index);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x12, 0x34 }, 0, (short)0x1234)]
    [DataRow(new byte[] { 0xAC, 0x03, 0xF0 }, 3, (short)-22504)] // 5 bits, 8 bits, then 3 bits
    public void ReadInt16Unaligned_ForBigEndian_ReturnsValue(byte[] bytes, int offset, short expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);
        bitReader.ReadBits(offset);

        // act
        short result = bitReader.ReadInt16Unaligned();

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x12, 0x34, 0x56, 0x78 }, 0, 0x12345678u)]
    [DataRow(new byte[] { 0xAC, 0x03, 0xF0, 0x5F, 0x12 }, 3, 0xA81F82FAu)] // 5 bits, 8, 8, 8, then 3 bits
    public void ReadUInt32Unaligned_ForBigEndian_ReturnsValue(byte[] bytes, int offset, uint expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);
        bitReader.ReadBits(offset);

        // act
        uint result = bitReader.ReadUInt32Unaligned();

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xFF, 0xFE }, 0, -2)]
    [DataRow(new byte[] { 0xAC, 0x03, 0xF0, 0x5F, 0x12 }, 3, unchecked((int)0xA81F82FA))]
    public void ReadInt32Unaligned_ForBigEndian_ReturnsValue(byte[] bytes, int offset, int expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);
        bitReader.ReadBits(offset);

        // act
        int result = bitReader.ReadInt32Unaligned();

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian)]
    [DataRow(EndianType.LittleEndian)]
    public void ReadUInt32Unaligned_AtAnyOffset_MatchesReadBits(EndianType endianType)
    {
        for (int offset = 0; offset < 16; offset++)
        {
            // arrange
            BitReader bitReader = new(_mixedBytes, endianType);
            BitReader bitReader2 = new(_mixedBytes, endianType);
            bitReader.ReadBits(offset);
            bitReader2.ReadBits(offset);

            // act
            uint result = bitReader.ReadUInt32Unaligned();

            // assert
            Assert.AreEqual(bitReader2.ReadBits(32), result, $"offset {offset}");
        }
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian, 0x0102030405060708ul)]
    [DataRow(EndianType.LittleEndian, 0x0807060504030201ul)]
    public void ReadUInt64Unaligned_WhenAligned_ReturnsValue(EndianType endianType, ulong expected)
    {
        // arrange
        BitReader bitReader = new([1, 2, 3, 4, 5, 6, 7, 8], endianType);

        // act
        ulong result = bitReader.ReadUInt64Unaligned();

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(EndianType.BigEndian)]
    [DataRow(EndianType.LittleEndian)]
    public void ReadUInt64AndInt64Unaligned_AtAnyOffset_MatchReadULongBits(EndianType endianType)
    {
        for (int offset = 0; offset < 16; offset++)
        {
            // arrange
            BitReader bitReader = new(_mixedBytes, endianType);
            BitReader bitReader2 = new(_mixedBytes, endianType);
            BitReader bitReader3 = new(_mixedBytes, endianType);
            bitReader.ReadBits(offset);
            bitReader2.ReadBits(offset);
            bitReader3.ReadBits(offset);

            // act
            ulong unsignedResult = bitReader.ReadUInt64Unaligned();
            long signedResult = bitReader2.ReadInt64Unaligned();

            // assert
            ulong expected = bitReader3.ReadULongBits(64);
            Assert.AreEqual(expected, unsignedResult, $"offset {offset}");
            Assert.AreEqual((long)expected, signedResult, $"offset {offset}");
        }
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00 }, 0L, 1)]
    [DataRow(new byte[] { 0x0A }, 5L, 1)]
    [DataRow(new byte[] { 0x7E }, 63L, 1)] // the most that fits in one byte
    [DataRow(new byte[] { 0xC8, 0x01 }, 100L, 2)]
    [DataRow(new byte[] { 0xA0, 0x9C, 0x01 }, 10000L, 3)]
    public void ReadVInt_ForPositiveValue_ReturnsValue(byte[] bytes, long expected, int expectedIndex)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);

        // act
        long result = bitReader.ReadVInt();

        // assert
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expectedIndex, bitReader.Index);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x0B }, 5L, 1)]
    [DataRow(new byte[] { 0x7F }, 63L, 1)]
    [DataRow(new byte[] { 0xC9, 0x01 }, 100L, 2)]
    [DataRow(new byte[] { 0xA1, 0x9C, 0x01 }, 10000L, 3)]
    public void ReadVInt_WithSignBitSet_ReturnsValueNotNegative(byte[] bytes, long expected, int expectedIndex)
    {
        // arrange
        // the lowest bit of the first byte is the sign bit, but the value is used as a size, so it's never negative
        BitReader bitReader = new(bytes, EndianType.BigEndian);

        // act
        long result = bitReader.ReadVInt();

        // assert
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expectedIndex, bitReader.Index);
    }

    [TestMethod]
    public void ReadVInt_TwoValues_ReadsEach()
    {
        // arrange
        BitReader bitReader = new([0x0A, 0xC8, 0x01], EndianType.BigEndian);

        // act
        long first = bitReader.ReadVInt();
        long second = bitReader.ReadVInt();

        // assert
        Assert.AreEqual(5L, first);
        Assert.AreEqual(100L, second);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xC8, 0x01, 0xFF }, new byte[] { 0xC8, 0x01 })]
    [DataRow(new byte[] { 0x0A, 0xFF }, new byte[] { 0x0A })]
    public void ReadBytesForVInt_ForVInt_ReturnsItsBytes(byte[] bytes, byte[] expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);

        // act
        byte[] result = bitReader.ReadBytesForVInt().ToArray();

        // assert
        Assert.AreSequenceEqual(expected, result);
        Assert.AreEqual(expected.Length, bitReader.Index);
    }

    [TestMethod]
    public void ReadAlignedByte_ForBytes_ReadsEachInOrder()
    {
        // arrange
        BitReader bitReader = new([1, 2, 3], EndianType.BigEndian);

        // act
        byte[] results = [bitReader.ReadAlignedByte(), bitReader.ReadAlignedByte(), bitReader.ReadAlignedByte()];

        // assert
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, results);
        Assert.AreEqual(3, bitReader.Index);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xAC, 0x03 }, 0, (byte)0xAC)]
    [DataRow(new byte[] { 0xAC, 0x03 }, 4, (byte)0xA3)] // the upper 4 bits of the first byte, then the lower 4 bits of the second
    public void ReadUnalignedByte_ForBigEndian_ReturnsByte(byte[] bytes, int offset, byte expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);
        bitReader.ReadBits(offset);

        // act
        byte result = bitReader.ReadUnalignedByte();

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ReadAlignedBytes_ForCount_ReturnsBytes()
    {
        // arrange
        BitReader bitReader = new([1, 2, 3, 4, 5], EndianType.BigEndian);

        // act
        byte[] result = bitReader.ReadAlignedBytes(3).ToArray();

        // assert
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, result);
        Assert.AreEqual(3, bitReader.Index);
    }

    [TestMethod]
    public void ReadAlignedBytes_AfterUnalignedRead_StartsAtNextByte()
    {
        // arrange
        BitReader bitReader = new([1, 2, 3, 4, 5], EndianType.BigEndian);
        bitReader.ReadBits(3);

        // act
        byte[] result = bitReader.ReadAlignedBytes(2).ToArray();

        // assert
        Assert.AreSequenceEqual(new byte[] { 2, 3 }, result);
        Assert.AreEqual(3, bitReader.Index);
    }

    [TestMethod]
    [DataRow(new byte[] { 0xAC, 0x03, 0x5F }, 0, new byte[] { 0xAC, 0x03 })]
    [DataRow(new byte[] { 0xAC, 0x03, 0x5F }, 4, new byte[] { 0xA3, 0x0F })]
    public void ReadUnalignedBytes_ForBigEndian_ReturnsBytes(byte[] bytes, int offset, byte[] expected)
    {
        // arrange
        BitReader bitReader = new(bytes, EndianType.BigEndian);
        bitReader.ReadBits(offset);

        // act
        byte[] result = bitReader.ReadUnalignedBytes(2).ToArray();

        // assert
        Assert.AreSequenceEqual(expected, result);
    }

    [TestMethod]
    public void ReadBytes_ForCount_ReturnsBytes()
    {
        // arrange
        BitReader bitReader = new([1, 2, 3, 4], EndianType.BigEndian);

        // act
        byte[] result = bitReader.ReadBytes(2).ToArray();

        // assert
        Assert.AreSequenceEqual(new byte[] { 1, 2 }, result);
        Assert.AreEqual(2, bitReader.Index);
    }

    [TestMethod]
    public void AlignToByte_InMiddleOfByte_MovesToNextByte()
    {
        // arrange
        BitReader bitReader = new([0xFF, 0x42], EndianType.BigEndian);
        bitReader.ReadBits(3);

        // act
        bitReader.AlignToByte();

        // assert
        Assert.AreEqual(0x42u, bitReader.ReadBits(8));
    }

    [TestMethod]
    public void AlignToByte_AtStartOfByte_DoesNotMove()
    {
        // arrange
        BitReader bitReader = new([0x11, 0x42], EndianType.BigEndian);
        bitReader.ReadBits(8);

        // act
        bitReader.AlignToByte();

        // assert
        Assert.AreEqual(0x42u, bitReader.ReadBits(8));
    }

    [TestMethod]
    public void BitReversementTest()
    {
        Span<byte> buffer = [8, 130, 67, 58, 92, 80, 114, 102, 0, 1, 12, 13, 115, 23, 28, 56, 47, 35];

        for (int bitsToRead = 1; bitsToRead < 33; bitsToRead++)
        {
            BitReader bitReader = new(buffer, EndianType.BigEndian);

            uint resultFirst = bitReader.ReadBits(bitsToRead);
            bitReader.BitReversement(bitsToRead);

            uint resultAfterReversement = bitReader.ReadBits(bitsToRead);

            Assert.AreEqual(resultAfterReversement, resultFirst);
        }
    }

    [TestMethod]
    public void BitReversementWithStartingOffsetTest()
    {
        Span<byte> buffer = [8, 130, 67, 58, 92, 80, 114, 102, 0, 1, 12, 13, 115, 23, 28, 56, 47, 35];

        for (int offset = 1; offset < 32; offset++)
        {
            for (int bitsToRead = 1; bitsToRead < 18; bitsToRead++)
            {
                BitReader bitReader = new(buffer, EndianType.BigEndian);
                bitReader.ReadBits(offset);

                uint resultFirst = bitReader.ReadBits(bitsToRead);
                bitReader.BitReversement(bitsToRead);

                uint resultAfterReversement = bitReader.ReadBits(bitsToRead);

                Assert.AreEqual(resultAfterReversement, resultFirst);
            }
        }
    }

    [TestMethod]
    public void BitReversement_ForLittleEndian_RereadsSameBits()
    {
        for (int offset = 0; offset < 32; offset++)
        {
            for (int numberOfBits = 1; numberOfBits <= 32; numberOfBits++)
            {
                // arrange
                BitReader bitReader = new(_mixedBytes, EndianType.LittleEndian);
                bitReader.ReadBits(offset);
                uint first = bitReader.ReadBits(numberOfBits);

                // act
                bitReader.BitReversement(numberOfBits);

                // assert
                Assert.AreEqual(first, bitReader.ReadBits(numberOfBits), $"offset {offset}, {numberOfBits} bits");
            }
        }
    }

    [TestMethod]
    public void BitReversement_ToStart_RereadsFirstByte()
    {
        // arrange
        BitReader bitReader = new([0xAC, 0x03], EndianType.BigEndian);
        bitReader.ReadBits(10);

        // act
        bitReader.BitReversement(10);

        // assert
        Assert.AreEqual(0xACu, bitReader.ReadBits(8));
    }

    [TestMethod]
    public void BitReversement_ZeroBits_DoesNotMove()
    {
        // arrange
        BitReader bitReader = new([0xAC, 0x03], EndianType.BigEndian);
        bitReader.ReadBits(3);

        // act
        bitReader.BitReversement(0);

        // assert
        Assert.AreEqual(675u, bitReader.ReadBits(10));
    }

    [TestMethod]
    [DataRow("s2m", new byte[4] { 115, 50, 109, 0 })]
    [DataRow("", new byte[4] { 0, 0, 0, 0 })]
    [DataRow("s2mv", new byte[4] { 115, 50, 109, 118 })]
    public void ReadStringFromBitsAsBigEndian(string result, byte[] bytes)
    {
        Span<byte> buffer = new(bytes);

        BitReader bitReader = new(buffer, EndianType.BigEndian);

        string value = bitReader.ReadStringFromBits(32);

        Assert.AreEqual(result, value);
    }

    [TestMethod]
    [DataRow("m2s", new byte[4] { 115, 50, 109, 0 })]
    [DataRow("", new byte[4] { 0, 0, 0, 0 })]
    [DataRow("vm2s", new byte[4] { 115, 50, 109, 118 })]
    public void ReadStringFromBitsAsLittleEndian(string result, byte[] bytes)
    {
        Span<byte> buffer = new(bytes);

        BitReader bitReader = new(buffer, EndianType.LittleEndian);

        string value = bitReader.ReadStringFromBits(32);

        Assert.AreEqual(result, value);
    }

    [TestMethod]
    [DataRow("s2m", 24)]
    [DataRow("s2mvabcd", 64)] // more than 32 bits
    public void ReadStringFromBits_ForNumberOfBits_ReturnsString(string expected, int numberOfBits)
    {
        // arrange
        BitReader bitReader = new(Encoding.UTF8.GetBytes(expected), EndianType.BigEndian);

        // act
        string result = bitReader.ReadStringFromBits(numberOfBits);

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void ReadStringFromBits_ZeroBits_ThrowsException()
    {
        // act / assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BitReader(new byte[8], EndianType.BigEndian).ReadStringFromBits(0));
    }

    [TestMethod]
    [DataRow("s2m", new byte[4] { 115, 50, 109, 0 })]
    [DataRow("", new byte[4] { 0, 0, 0, 0 })]
    [DataRow("s2mv", new byte[4] { 115, 50, 109, 118 })]
    public void ReadStringFromAlignedBytesAsBigEndian(string result, byte[] bytes)
    {
        Span<byte> buffer = new(bytes);

        BitReader bitReader = new(buffer, EndianType.BigEndian);

        string value = bitReader.ReadStringFromAlignedBytes(4);

        Assert.AreEqual(result, value);
    }

    [TestMethod]
    [DataRow("m2s", new byte[4] { 115, 50, 109, 0 })]
    [DataRow("", new byte[4] { 0, 0, 0, 0 })]
    [DataRow("vm2s", new byte[4] { 115, 50, 109, 118 })]
    public void ReadStringFromAlignedBytesAsLittleEndian(string result, byte[] bytes)
    {
        Span<byte> buffer = new(bytes);

        BitReader bitReader = new(buffer, EndianType.LittleEndian);

        string value = bitReader.ReadStringFromAlignedBytes(4);

        Assert.AreEqual(result, value);
    }

    [TestMethod]
    public void ReadStringFromAlignedBytes_AfterUnalignedRead_StartsAtNextByte()
    {
        // arrange
        BitReader bitReader = new("xs2mv"u8, EndianType.BigEndian);
        bitReader.ReadBits(3);

        // act
        string result = bitReader.ReadStringFromAlignedBytes(4);

        // assert
        Assert.AreEqual("s2mv", result);
    }

    [TestMethod]
    public void ReadStringFromAlignedBytes_ZeroBytes_ThrowsException()
    {
        // act / assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BitReader(new byte[8], EndianType.BigEndian).ReadStringFromAlignedBytes(0));
    }

    [TestMethod]
    public void ReadBlobAsString()
    {
        Span<byte> buffer = [4, 115, 50, 109, 118];

        BitReader bitReader = new(buffer, EndianType.BigEndian);

        string value = bitReader.ReadBlobAsString(8);

        Assert.AreEqual("s2mv", value);
    }

    [TestMethod]
    public void ReadBlobAsString_WithAdditionalBytes_ReadsMoreBytes()
    {
        // arrange
        BitReader bitReader = new([3, 115, 50, 109, 118], EndianType.BigEndian);

        // act
        string result = bitReader.ReadBlobAsString(8, additionalBytesToRead: 1);

        // assert
        Assert.AreEqual("s2mv", result);
    }

    [TestMethod]
    public void ReadBlobAsString_LengthOver32Bits_ReturnsString()
    {
        // arrange
        BitReader bitReader = new([0, 0, 0, 0, 4, 115, 50, 109, 118], EndianType.BigEndian);

        // act
        string result = bitReader.ReadBlobAsString(40);

        // assert
        Assert.AreEqual("s2mv", result);
    }

    [TestMethod]
    public void ReadBlobAsString_ZeroBits_ThrowsException()
    {
        // act / assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BitReader(new byte[8], EndianType.BigEndian).ReadBlobAsString(0));
    }

    [TestMethod]
    public void ReadStringFromUnalignedBytesAsBigEndian()
    {
        Span<byte> buffer = [108, 211, 66, 162, 55];

        BitReader bitReader = new(buffer, EndianType.BigEndian);

        bitReader.ReadBits(5);

        string value = bitReader.ReadStringFromUnalignedBytes(4);

        Assert.AreEqual("zhTW", value);
    }

    [TestMethod]
    public void ReadStringFromUnalignedBytesAsLittleEndian()
    {
        Span<byte> buffer = [108, 211, 66, 162, 55];

        BitReader bitReader = new(buffer, EndianType.LittleEndian);

        bitReader.ReadBits(5);

        string value = bitReader.ReadStringFromUnalignedBytes(4);

        Assert.AreEqual("WThz", value);
    }

    [TestMethod]
    public void ReadStringFromUnalignedBytes_ZeroBytes_ThrowsException()
    {
        // act / assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BitReader(new byte[8], EndianType.BigEndian).ReadStringFromUnalignedBytes(0));
    }
}

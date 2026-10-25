using System.Security.Cryptography;

namespace Heroes.MpqTool.Tests;

[TestClass]
public class MpqHeroesArchiveTests
{
    private readonly string _mpqDirectory = "HeroesMpqFiles";
    private readonly string _s2maFile1 = "mapModFile1.s2ma";
    private readonly string _replayFile1 = "replayFile1.StormR";
    private readonly string _replayFile2 = "replayFile2.StormR";
    private readonly string _replayDetailsEntry = "replay.details";
    private readonly string _waterDataEntry = "Base.StormData\\GameData\\WaterData.xml";

    // Entries covering each way an entry can be stored: the file, the entry, its decompressed size, and the SHA-256 of its decompressed data.
    public static IEnumerable<object[]> KnownEntries =>
    [
        ["replayFile1.StormR", "replay.details", 642, "E8F7E37161DB7DC707B8E710CD463BAE3C13CE448F3D8D4F51607AEAEED4B312"], // stored uncompressed
        ["replayFile1.StormR", "replay.initData", 14891, "64496F4CF8D0198218C7A06BC3DF9F760DA3BED291A3182FE6D58FB3AFC8F5C5"], // bzip2, single unit
        ["replayFile1.StormR", "replay.message.events", 0, "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"], // empty
        ["replayFile1.StormR", "(attributes)", 288, "94AD7F4421A28DA2E025062157ED51AB943496BA1B7989D213C7B664EEC57AA5"], // not in the listfile
        ["replayFile2.StormR", "replay.details", 1274, "EB3511C8CF522FAF95E7790E27F4A35EDE45CA37E3A8B16DE98C84D5B516D7F7"], // bzip2, single unit
        ["replayFile2.StormR", "replay.tracker.events", 8386122, "5DE16592E75922B53047EA55C903EEFFB85165DD6C2952015C64C370836E95FE"], // bzip2, in blocks
        ["mapModFile1.s2ma", "MapInfo", 1071, "96E48798EB100BE3E9CCF46B92B43A4BAA1F07A1471A3FEB113645DBD672C70F"], // zlib, single unit
        ["mapModFile1.s2ma", "Base.StormData\\GameData\\WaterData.xml", 986, "FBD0B0AFB22B6A0CD0504670239244CDABCAF4536C69B44B79CDF8FEB2A08976"], // zlib, single unit
        ["mapModFile1.s2ma", "MapScript.galaxy", 22354, "ACD7B05A7E29FF9E8E9B28236F5C0C1191D2044DD99965077B2FEDDD5E5F4B59"], // zlib, in blocks
        ["mapModFile1.s2ma", "t3CellFlags", 51616, "9DC5E0A47857BE257AB17711FB9C1F37618FE7E8B737440738E2610C513AC5A0"], // zlib, in blocks
        ["mapModFile1.s2ma", "(listfile)", 1680, "D66C4917F6299C34DBFE099943D7889687F373E13D1138BF135AC2F73E5A92AB"], // zlib, single unit
    ];

    [TestMethod]
    [DataRow("replayFile1.StormR", 14)]
    [DataRow("replayFile2.StormR", 14)]
    [DataRow("mapModFile1.s2ma", 66)]
    public void MpqArchiveEntries_ForFile_HasEveryEntry(string file, int expectedCount)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(file);

        // act
        MpqHeroesArchiveEntry[] entries = mpqHeroesArchive.MpqArchiveEntries;

        // assert
        Assert.HasCount(expectedCount, entries);
        Assert.IsTrue(entries.All(entry => entry.Exists));
    }

    [TestMethod]
    [DataRow("replayFile1.StormR")]
    [DataRow("replayFile2.StormR")]
    [DataRow("mapModFile1.s2ma")]
    public void MpqArchiveEntries_NamedEntries_MatchEntriesFromGetEntry(string file)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(file);

        foreach (MpqHeroesArchiveEntry entry in mpqHeroesArchive.MpqArchiveEntries.Where(entry => entry.FileName is not null))
        {
            // act
            MpqHeroesArchiveEntry lookedUp = mpqHeroesArchive.GetEntry(entry.FileName!);

            // assert
            Assert.AreEqual(entry.FileName, lookedUp.FileName);
            Assert.AreEqual(entry.FileSize, lookedUp.FileSize);
            Assert.AreEqual(entry.CompressedSize, lookedUp.CompressedSize);
            Assert.AreEqual(entry.Flags, lookedUp.Flags);
        }
    }

    [TestMethod]
    public void GetEntry_ForFileNameWithStream_ReturnsEntry()
    {
        // arrange
        using FileStream fileStream = File.OpenRead(Path.Join(_mpqDirectory, _replayFile1));
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(fileStream);

        // act
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_replayDetailsEntry);

        // assert
        Assert.AreEqual(_replayDetailsEntry, entry.FileName);
        Assert.AreEqual(642u, entry.CompressedSize);
        Assert.AreEqual(642u, entry.FileSize);
    }

    [TestMethod]
    public void GetEntry_ForFileName_ReturnsEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile2);

        // act
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_replayDetailsEntry);

        // assert
        Assert.AreEqual(_replayDetailsEntry, entry.FileName);
        Assert.AreEqual(979u, entry.CompressedSize);
        Assert.AreEqual(1274u, entry.FileSize);
        Assert.IsTrue(entry.IsCompressed);
        Assert.IsTrue(entry.IsSingleUnit);
        Assert.IsFalse(entry.IsEncrypted);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("blank")]
    public void GetEntry_ForNoFileName_ThrowsException(string fileName)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() => mpqHeroesArchive.GetEntry(fileName));

        // assert
        Assert.AreEqual(fileName, exception.FileName);
    }

    [TestMethod]
    [DataRow("REPLAY.DETAILS")]
    [DataRow("Replay.Details")]
    public void GetEntry_DifferentCase_ReturnsEntry(string fileName)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(fileName);

        // assert
        Assert.AreEqual(_replayDetailsEntry, entry.FileName);
    }

    [TestMethod]
    public void GetEntry_ForSpanFileName_ReturnsEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // the name is only part of the string
        ReadOnlySpan<char> fileName = $"[{_replayDetailsEntry}]".AsSpan(1, _replayDetailsEntry.Length);

        // act
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(fileName);

        // assert
        Assert.AreEqual(_replayDetailsEntry, entry.FileName);
        Assert.AreEqual(642u, entry.FileSize);
    }

    [TestMethod]
    public void GetEntry_ForSpanNoFileName_ThrowsException()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        FileNotFoundException exception = Assert.ThrowsExactly<FileNotFoundException>(() => mpqHeroesArchive.GetEntry("blank".AsSpan()));

        // assert
        Assert.AreEqual("blank", exception.FileName);
    }

    [TestMethod]
    public void GetEntry_WithNormalizeSlashes_ForwardSlashEqualsBackslash()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        MpqHeroesArchiveEntry entryBackslash = mpqHeroesArchive.GetEntry(_waterDataEntry);
        MpqHeroesArchiveEntry entryForwardSlash = mpqHeroesArchive.GetEntry(_waterDataEntry.Replace('\\', '/'), normalizeSlashes: true);

        // assert
        Assert.AreEqual(_waterDataEntry, entryBackslash.FileName);
        Assert.AreEqual(entryBackslash.FileName, entryForwardSlash.FileName);
        Assert.AreEqual(entryBackslash.FileSize, entryForwardSlash.FileSize);
    }

    [TestMethod]
    public void GetEntry_WithNormalizeSlashesFalse_ForwardSlashThrowsException()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        object? Action() => mpqHeroesArchive.GetEntry(_waterDataEntry.Replace('\\', '/'), normalizeSlashes: false);

        // assert
        Assert.ThrowsExactly<FileNotFoundException>(Action);
    }

    [TestMethod]
    public void GetEntry_ForSpanWithNormalizeSlashes_ForwardSlashEqualsBackslash()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_waterDataEntry.Replace('\\', '/').AsSpan(), normalizeSlashes: true);

        // assert
        Assert.AreEqual(_waterDataEntry, entry.FileName);
    }

    [TestMethod]
    public void TryGetEntry_ForFileName_ReturnsEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        bool result = mpqHeroesArchive.TryGetEntry(_replayDetailsEntry, out MpqHeroesArchiveEntry? entry);

        // assert
        Assert.IsTrue(result);
        Assert.IsNotNull(entry);
        Assert.AreEqual(_replayDetailsEntry, entry.Value.FileName);
        Assert.AreEqual(642u, entry.Value.FileSize);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("blank")]
    public void TryGetEntry_ForNoFileName_NoEntry(string fileName)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        bool result = mpqHeroesArchive.TryGetEntry(fileName, out MpqHeroesArchiveEntry? entry);

        // assert
        Assert.IsFalse(result);
        Assert.IsNull(entry);
    }

    [TestMethod]
    public void TryGetEntry_DifferentCase_ReturnsEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.TryGetEntry(_waterDataEntry.ToLowerInvariant(), out MpqHeroesArchiveEntry? entry);

        // assert
        Assert.IsTrue(result);
        Assert.IsNotNull(entry);
        Assert.AreEqual(_waterDataEntry, entry.Value.FileName);
    }

    [TestMethod]
    public void TryGetEntry_ForFileNotInListfile_ReturnsEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        bool result = mpqHeroesArchive.TryGetEntry("(attributes)", out MpqHeroesArchiveEntry? entry);

        // assert
        Assert.IsTrue(result);
        Assert.IsNotNull(entry);
        Assert.AreEqual(288u, entry.Value.FileSize);
    }

    [TestMethod]
    public void TryGetEntry_ForSpanFileName_ReturnsEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        Span<char> fileName = stackalloc char[_replayDetailsEntry.Length];
        _replayDetailsEntry.CopyTo(fileName);

        // act
        bool result = mpqHeroesArchive.TryGetEntry(fileName, out MpqHeroesArchiveEntry? entry);

        // assert
        Assert.IsTrue(result);
        Assert.IsNotNull(entry);
        Assert.AreEqual(_replayDetailsEntry, entry.Value.FileName);
    }

    [TestMethod]
    public void TryGetEntry_ForSpanNoFileName_NoEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        bool result = mpqHeroesArchive.TryGetEntry("blank".AsSpan(), out MpqHeroesArchiveEntry? entry);

        // assert
        Assert.IsFalse(result);
        Assert.IsNull(entry);
    }

    [TestMethod]
    public void TryGetEntry_WithNormalizeSlashes_ForwardSlashEqualsBackslash()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.TryGetEntry(_waterDataEntry.Replace('\\', '/'), out MpqHeroesArchiveEntry? entry, normalizeSlashes: true);

        // assert
        Assert.IsTrue(result);
        Assert.IsNotNull(entry);
        Assert.AreEqual(_waterDataEntry, entry.Value.FileName);
    }

    [TestMethod]
    public void TryGetEntry_WithNormalizeSlashesFalse_ForwardSlashReturnsNoEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.TryGetEntry(_waterDataEntry.Replace('\\', '/'), out MpqHeroesArchiveEntry? entry, normalizeSlashes: false);

        // assert
        Assert.IsFalse(result);
        Assert.IsNull(entry);
    }

    [TestMethod]
    public void TryGetEntry_ForSpanWithNormalizeSlashes_ForwardSlashEqualsBackslash()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);
        ReadOnlySpan<char> fileName = _waterDataEntry.Replace('\\', '/').AsSpan();

        // act
        bool resultNormalized = mpqHeroesArchive.TryGetEntry(fileName, out MpqHeroesArchiveEntry? entry, normalizeSlashes: true);
        bool resultNotNormalized = mpqHeroesArchive.TryGetEntry(fileName, out _, normalizeSlashes: false);

        // assert
        Assert.IsTrue(resultNormalized);
        Assert.IsNotNull(entry);
        Assert.AreEqual(_waterDataEntry, entry.Value.FileName);
        Assert.IsFalse(resultNotNormalized);
    }

    [TestMethod]
    public void FileEntryExists_ForReplayFileExistingEntry_ReturnsTrue()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile2);

        // act
        bool result = mpqHeroesArchive.FileEntryExists(_replayDetailsEntry);

        // assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FileEntryExists_ForReplayFileNonExistingEntry_ReturnsFalse()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile2);

        // act
        bool result = mpqHeroesArchive.FileEntryExists("blank");

        // assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void FileEntryExists_ForS2maFileExistingEntry_ReturnsTrue()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.FileEntryExists("t3CellFlags");

        // assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FileEntryExists_ForS2maFileNonExistingEntry_ReturnsFalse()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.FileEntryExists("blank");

        // assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void FileEntryExists_EmptyFileName_ReturnsFalse()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.FileEntryExists(string.Empty);

        // assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("T3CELLFLAGS")]
    [DataRow("t3cellflags")]
    public void FileEntryExists_DifferentCase_ReturnsTrue(string fileName)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.FileEntryExists(fileName);

        // assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FileEntryExists_ForFileNotInListfile_ReturnsTrue()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.FileEntryExists("(attributes)");

        // assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow("t3CellFlags", true)]
    [DataRow("blank", false)]
    [DataRow("", false)]
    public void FileEntryExists_ForSpanFileName_ReturnsWhetherEntryExists(string fileName, bool expected)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool result = mpqHeroesArchive.FileEntryExists(fileName.AsSpan());

        // assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void FileEntryExists_WithNormalizeSlashes_ForwardSlashEqualsBackslash()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);

        // act
        bool resultNormalized = mpqHeroesArchive.FileEntryExists(_waterDataEntry.Replace('\\', '/'), normalizeSlashes: true);
        bool resultNotNormalized = mpqHeroesArchive.FileEntryExists(_waterDataEntry.Replace('\\', '/'), normalizeSlashes: false);

        // assert
        Assert.IsTrue(resultNormalized);
        Assert.IsFalse(resultNotNormalized);
    }

    [TestMethod]
    public void FileEntryExists_ForSpanWithNormalizeSlashes_ForwardSlashEqualsBackslash()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);
        ReadOnlySpan<char> fileName = _waterDataEntry.Replace('\\', '/').AsSpan();

        // act
        bool resultNormalized = mpqHeroesArchive.FileEntryExists(fileName, normalizeSlashes: true);
        bool resultNotNormalized = mpqHeroesArchive.FileEntryExists(fileName, normalizeSlashes: false);

        // assert
        Assert.IsTrue(resultNormalized);
        Assert.IsFalse(resultNotNormalized);
    }

    [TestMethod]
    [DynamicData(nameof(KnownEntries))]
    public void DecompressEntry_AsStream_ReturnsEntryData(string file, string entryName, int expectedSize, string expectedSha256)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(file);
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(entryName);

        // act
        using Stream stream = mpqHeroesArchive.DecompressEntry(entry);

        // assert
        Assert.AreEqual(expectedSize, stream.Length);
        Assert.AreEqual(0, stream.Position);
        Assert.AreEqual(expectedSha256, Sha256(ReadAll(stream)));
    }

    [TestMethod]
    [DynamicData(nameof(KnownEntries))]
    public void DecompressEntry_AsBuffer_WritesEntryData(string file, string entryName, int expectedSize, string expectedSha256)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(file);
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(entryName);

        byte[] buffer = new byte[expectedSize];

        // act
        mpqHeroesArchive.DecompressEntry(entry, buffer);

        // assert
        Assert.AreEqual(expectedSha256, Sha256(buffer));
    }

    [TestMethod]
    [DynamicData(nameof(KnownEntries))]
    public void DecompressEntry_AsLargerBuffer_WritesEntryDataAtStart(string file, string entryName, int expectedSize, string expectedSha256)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(file);
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(entryName);

        // like a buffer from ArrayPool, which can be larger than asked for
        byte[] buffer = new byte[expectedSize + 100];
        buffer.AsSpan().Fill(0xCC);

        // act
        mpqHeroesArchive.DecompressEntry(entry, buffer);

        // assert
        Assert.AreEqual(expectedSha256, Sha256(buffer.AsSpan(0, expectedSize)));
        Assert.AreEqual(-1, buffer.AsSpan(expectedSize).IndexOfAnyExcept((byte)0xCC), "The rest of the buffer was written to");
    }

    [TestMethod]
    [DynamicData(nameof(KnownEntries))]
    public void DecompressEntry_ArchiveOpenedFromMemoryStream_ReturnsEntryData(string file, string entryName, int expectedSize, string expectedSha256)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(new MemoryStream(File.ReadAllBytes(Path.Join(_mpqDirectory, file)), writable: false));
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(entryName);

        // act
        using Stream stream = mpqHeroesArchive.DecompressEntry(entry);

        // assert
        Assert.AreEqual(expectedSize, stream.Length);
        Assert.AreEqual(expectedSha256, Sha256(ReadAll(stream)));
    }

    [TestMethod]
    [DataRow("replayFile1.StormR")]
    [DataRow("replayFile2.StormR")]
    [DataRow("mapModFile1.s2ma")]
    public void DecompressEntry_EveryEntryInReverseOrder_StreamAndBufferMatch(string file)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(file);

        foreach (MpqHeroesArchiveEntry entry in Enumerable.Reverse(mpqHeroesArchive.MpqArchiveEntries))
        {
            byte[] buffer = new byte[entry.FileSize];

            // act
            using Stream stream = mpqHeroesArchive.DecompressEntry(entry);
            mpqHeroesArchive.DecompressEntry(entry, buffer);

            // assert
            Assert.IsTrue(buffer.AsSpan().SequenceEqual(ReadAll(stream)), $"Mismatch for {entry}");
        }
    }

    [TestMethod]
    public void DecompressEntry_AsStream_IsReadOnly()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_replayDetailsEntry);

        // act
        using Stream stream = mpqHeroesArchive.DecompressEntry(entry);

        // assert
        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.WriteByte(0));
    }

    [TestMethod]
    public void DecompressEntry_AsStream_ExposesBufferWithoutCopying()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry("MapInfo");

        // act
        using Stream stream = mpqHeroesArchive.DecompressEntry(entry);

        // assert
        MemoryStream? memoryStream = stream as MemoryStream;
        Assert.IsNotNull(memoryStream);
        Assert.IsTrue(memoryStream.TryGetBuffer(out ArraySegment<byte> buffer));
        Assert.AreEqual("96E48798EB100BE3E9CCF46B92B43A4BAA1F07A1471A3FEB113645DBD672C70F", Sha256(buffer));
    }

    [TestMethod]
    public void DecompressEntry_AfterDispose_ThrowsException()
    {
        // arrange
        MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_replayDetailsEntry);

        mpqHeroesArchive.Dispose();

        // act / assert
        Assert.ThrowsExactly<ObjectDisposedException>(() => mpqHeroesArchive.DecompressEntry(entry));
    }

    [TestMethod]
    [DataRow("replayFile1.StormR", (byte)0x1B)] // replays start with a user data header
    [DataRow("replayFile2.StormR", (byte)0x1B)]
    [DataRow("mapModFile1.s2ma", (byte)0x1A)]
    public void GetHeaderBytes_ForFile_ReturnsStream(string file, byte expectedFourthByte)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(file);

        // act
        using Stream stream = mpqHeroesArchive.GetHeaderBytes();

        // assert
        Assert.AreEqual(MpqHeroesArchive.HeaderSize, stream.Length);
        Assert.AreEqual(0, stream.Position);

        byte[] header = ReadAll(stream);
        CollectionAssert.AreEqual(new byte[] { (byte)'M', (byte)'P', (byte)'Q', expectedFourthByte }, header[..4]);
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Join(_mpqDirectory, file))[..MpqHeroesArchive.HeaderSize], header);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(1024)]
    public void GetHeaderBytes_WithSize_ReturnsThatManyBytes(int size)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        using Stream stream = mpqHeroesArchive.GetHeaderBytes(size);

        // assert
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Join(_mpqDirectory, _replayFile1))[..size], ReadAll(stream));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void GetHeaderBytes_InvalidSize_ThrowsException(int size)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile2);

        // act
        object? Action() => mpqHeroesArchive.GetHeaderBytes(size);

        // assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(Action);
    }

    [TestMethod]
    public void GetHeaderBytes_ReturnedStream_IsReadOnly()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_replayFile1);

        // act
        using Stream stream = mpqHeroesArchive.GetHeaderBytes();

        // assert
        Assert.IsFalse(stream.CanWrite);
    }

    [TestMethod]
    [DataRow("replayFile2.StormR")]
    [DataRow("mapModFile1.s2ma")]
    public void GetHeaderBytes_ForFile_GetsReadIntoBuffer(string file)
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(file);
        byte[] expected = File.ReadAllBytes(Path.Join(_mpqDirectory, file))[..MpqHeroesArchive.HeaderSize];

        // act
        Span<byte> buffer = stackalloc byte[MpqHeroesArchive.HeaderSize];
        mpqHeroesArchive.GetHeaderBytes(buffer);

        // assert
        Assert.IsTrue(buffer.SequenceEqual(expected));
    }

    [TestMethod]
    public void GetHeaderBytes_AfterDecompressingEntry_ReadsFromStartOfFile()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = OpenArchive(_s2maFile1);
        byte[] expected = File.ReadAllBytes(Path.Join(_mpqDirectory, _s2maFile1))[..MpqHeroesArchive.HeaderSize];

        mpqHeroesArchive.DecompressEntry(mpqHeroesArchive.GetEntry("t3CellFlags")).Dispose();

        // act
        using Stream stream = mpqHeroesArchive.GetHeaderBytes();
        Span<byte> buffer = stackalloc byte[MpqHeroesArchive.HeaderSize];
        mpqHeroesArchive.GetHeaderBytes(buffer);

        // assert
        CollectionAssert.AreEqual(expected, ReadAll(stream));
        Assert.IsTrue(buffer.SequenceEqual(expected));
    }

    [TestMethod]
    public void Dispose_ArchiveOpenedFromStream_DisposesStream()
    {
        // arrange
        FileStream fileStream = File.OpenRead(Path.Join(_mpqDirectory, _replayFile1));
        MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(fileStream);

        // act
        mpqHeroesArchive.Dispose();

        // assert
        Assert.IsFalse(fileStream.CanRead);
    }

    [TestMethod]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // arrange
        FileStream fileStream = File.OpenRead(Path.Join(_mpqDirectory, _replayFile1));
        MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(fileStream);

        // act
        mpqHeroesArchive.Dispose();
        mpqHeroesArchive.Dispose();

        // assert
        Assert.IsFalse(fileStream.CanRead);
    }

    private static string Sha256(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data));

    private static byte[] ReadAll(Stream stream)
    {
        using MemoryStream copy = new();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    private MpqHeroesArchive OpenArchive(string file) => MpqHeroesFile.Open(Path.Join(_mpqDirectory, file));
}
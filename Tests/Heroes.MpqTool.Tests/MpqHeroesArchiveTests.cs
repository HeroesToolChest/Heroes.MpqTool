namespace Heroes.MpqTool.Tests;

[TestClass]
public class MpqHeroesArchiveTests
{
    private readonly string _mpqDirectory = "HeroesMpqFiles";
    private readonly string _s2maFile1 = "mapModFile1.s2ma";
    private readonly string _replayFile1 = "replayFile1.StormR";
    private readonly string _replayFile2 = "replayFile2.StormR";
    private readonly string _replayDetailsEntry = "replay.details";
    private readonly string _replayTrackerEventsEntry = "replay.tracker.events";

    [TestMethod]
    public void GetEntry_ForFileName_ReturnsEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile1));

        // act
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_replayDetailsEntry);

        // assert
        Assert.AreEqual(642u, entry.CompressedSize);
    }

    [TestMethod]
    public void GetEntry_ForNoFileName_ThrowsException()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile1));

        // act / assert
        Assert.ThrowsException<FileNotFoundException>(() => mpqHeroesArchive.GetEntry(string.Empty));
    }

    [TestMethod]
    public void TryGetEntry_ForFileName_ReturnsEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile1));

        // act
        bool result = mpqHeroesArchive.TryGetEntry(_replayDetailsEntry, out MpqHeroesArchiveEntry? entry);

        // assert
        Assert.IsTrue(result);
        Assert.IsNotNull(entry);
        Assert.AreEqual(_replayDetailsEntry, entry.Value.FileName);
    }

    [TestMethod]
    public void TryGetEntry_ForNoFileName_NoEntry()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile1));

        // act
        bool result = mpqHeroesArchive.TryGetEntry(string.Empty, out MpqHeroesArchiveEntry? entry);

        // assert
        Assert.IsFalse(result);
        Assert.IsNull(entry);
    }

    [TestMethod]
    public void DecompressEntry_AsBuffer_ReturnsInSpanParameter()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile1));
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_replayDetailsEntry);

        Span<byte> buffer = stackalloc byte[(int)entry.FileSize];

        // act
        mpqHeroesArchive.DecompressEntry(entry, buffer);

        // assert
        Assert.AreEqual((int)entry.FileSize, buffer.Length);
    }

    [TestMethod]
    public void DecompressEntry_AsStream_ReturnsStream()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile1));
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_replayDetailsEntry);

        // act
        Stream stream = mpqHeroesArchive.DecompressEntry(entry);

        // assert
        Assert.AreEqual((int)entry.FileSize, stream.Length);
        Assert.AreEqual(0, stream.Position);
    }

    [TestMethod]
    public void DecompressEntry_WithCompressNonSingleUnitEntry_ReturnsStream()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile2));
        MpqHeroesArchiveEntry entry = mpqHeroesArchive.GetEntry(_replayTrackerEventsEntry);

        // act
        Stream stream = mpqHeroesArchive.DecompressEntry(entry);

        // assert
        Assert.AreEqual((int)entry.FileSize, stream.Length);
        Assert.AreEqual(0, stream.Position);
    }

    [TestMethod]
    public void FileEntryExists_ForReplayFileExistingEntry_ReturnsTrue()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile2));

        // act
        bool result = mpqHeroesArchive.FileEntryExists(_replayDetailsEntry);

        // assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FileEntryExists_ForReplayFileNonExistingEntry_ReturnsFalse()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _replayFile2));

        // act
        bool result = mpqHeroesArchive.FileEntryExists("blank");

        // assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void FileEntryExists_ForS2maFileExistingEntry_ReturnsTrue()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _s2maFile1));

        // act
        bool result = mpqHeroesArchive.FileEntryExists("t3CellFlags");

        // assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void FileEntryExists_ForS2maFileNonExistingEntry_ReturnsFalse()
    {
        // arrange
        using MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, _s2maFile1));

        // act
        bool result = mpqHeroesArchive.FileEntryExists("blank");

        // assert
        Assert.IsFalse(result);
    }
}
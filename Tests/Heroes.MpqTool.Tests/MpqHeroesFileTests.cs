namespace Heroes.MpqTool.Tests;

[TestClass]
public class MpqHeroesFileTests
{
    private readonly string _mpqDirectory = "HeroesMpqFiles";

    [TestMethod]
    public void OpenForReplayFile1Test()
    {
        MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, "replayFile1.StormR"));

        Assert.AreEqual(14, mpqHeroesArchive.MpqArchiveEntries.Length);
        Assert.AreEqual("replay.details", mpqHeroesArchive.MpqArchiveEntries[0].FileName);
        Assert.AreEqual("replay.initData", mpqHeroesArchive.MpqArchiveEntries[1].FileName);
        Assert.AreEqual("replay.server.battlelobby", mpqHeroesArchive.MpqArchiveEntries[2].FileName);
        Assert.AreEqual("replay.game.events", mpqHeroesArchive.MpqArchiveEntries[3].FileName);
        Assert.AreEqual("replay.message.events", mpqHeroesArchive.MpqArchiveEntries[4].FileName);
        Assert.AreEqual("replay.load.info", mpqHeroesArchive.MpqArchiveEntries[5].FileName);
        Assert.AreEqual("replay.sync.events", mpqHeroesArchive.MpqArchiveEntries[6].FileName);
    }

    [TestMethod]
    public void OpenForS2maFile1Test()
    {
        MpqHeroesArchive mpqHeroesArchive = MpqHeroesFile.Open(Path.Join(_mpqDirectory, "mapModFile1.s2ma"));

        Assert.AreEqual(66, mpqHeroesArchive.MpqArchiveEntries.Length);
        Assert.AreEqual("t3CellFlags", mpqHeroesArchive.MpqArchiveEntries[0].FileName);
        Assert.AreEqual("DocumentInfo.version", mpqHeroesArchive.MpqArchiveEntries[1].FileName);
        Assert.AreEqual("CellAttribute_Pnp", mpqHeroesArchive.MpqArchiveEntries[2].FileName);
        Assert.AreEqual("CellAttribute_Pde", mpqHeroesArchive.MpqArchiveEntries[3].FileName);
        Assert.AreEqual("Base.StormData\\GameData\\GameUIData.xml", mpqHeroesArchive.MpqArchiveEntries[4].FileName);
        Assert.AreEqual("Objects", mpqHeroesArchive.MpqArchiveEntries[5].FileName);
        Assert.AreEqual("MapInfo", mpqHeroesArchive.MpqArchiveEntries[6].FileName);
    }
}

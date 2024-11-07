using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Heroes.MpqTool.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class MpqBenchmarks
{
    private readonly string _replaysFolder = "Replays";
    private readonly string _replayFile = "replayFile2.StormR";
    private readonly string _s2maFile = "mapModFile1.s2ma";

    [Benchmark]
    public void ParseReplay()
    {
        MpqHeroesFile.Open(Path.Join(_replaysFolder, _replayFile));
    }

    [Benchmark]
    public void ParseS2ma()
    {
        MpqHeroesFile.Open(Path.Join(_replaysFolder, _s2maFile));
    }
}

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Heroes.MpqTool.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net60)]
[SimpleJob(RuntimeMoniker.Net80)]
public class MpqBenchmarks
{
    private readonly string _replaysFolder = "Replays";
    private readonly string _replayFile2 = "replayFile2.StormR";

    [Benchmark]
    public void ParseReplay()
    {
        MpqHeroesFile.Open(Path.Join(_replaysFolder, _replayFile2));
    }
}

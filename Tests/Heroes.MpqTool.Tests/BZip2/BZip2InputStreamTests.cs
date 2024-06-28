// original https://github.com/DinoChiesa/DotNetZip/blob/master/BZip2%20Tests/BZip2UnitTest1.cs

namespace Heroes.MpqTool.Tests.BZip2;

[TestClass]
public class BZip2InputStreamTests
{
    private readonly Random _random;

    private readonly Dictionary<string, string> _testStrings = new()
    {
        { "LetMeDoItNow", "I expect to pass through the world but once. Any good therefore that I can do, or any kindness I can show to any creature, let me do it now. Let me not defer it, for I shall not pass this way again. -- Anonymous, although some have attributed it to Stephen Grellet" },
        { "UntilHeExtends", "Until he extends the circle of his compassion to all living things, man will not himself find peace. - Albert Schweitzer, early 20th-century German Nobel Peace Prize-winning mission doctor and theologian." },
        { "WhatWouldThingsHaveBeenLike", "'What would things have been like [in Russia] if during periods of mass arrests people had not simply sat there, paling with terror at every bang on the downstairs door and at every step on the staircase, but understood they had nothing to lose and had boldly set up in the downstairs hall an ambush of half a dozen people?' -- Alexander Solzhenitsyn" },
        {
            "GoPlacidly",

            @"Go placidly amid the noise and haste, and remember what peace there may be in silence.

As far as possible, without surrender, be on good terms with all persons. Speak your truth quietly and clearly; and listen to others, even to the dull and the ignorant, they too have their story. Avoid loud and aggressive persons, they are vexations to the spirit.

If you compare yourself with others, you may become vain and bitter; for always there will be greater and lesser persons than yourself. Enjoy your achievements as well as your plans. Keep interested in your own career, however humble; it is a real possession in the changing fortunes of time.

Exercise caution in your business affairs, for the world is full of trickery. But let this not blind you to what virtue there is; many persons strive for high ideals, and everywhere life is full of heroism. Be yourself. Especially, do not feign affection. Neither be cynical about love, for in the face of all aridity and disenchantment it is perennial as the grass.

Take kindly to the counsel of the years, gracefully surrendering the things of youth. Nurture strength of spirit to shield you in sudden misfortune. But do not distress yourself with imaginings. Many fears are born of fatigue and loneliness.

Beyond a wholesome discipline, be gentle with yourself. You are a child of the universe, no less than the trees and the stars; you have a right to be here. And whether or not it is clear to you, no doubt the universe is unfolding as it should.

Therefore be at peace with God, whatever you conceive Him to be, and whatever your labors and aspirations, in the noisy confusion of life, keep peace in your soul.

With all its sham, drudgery and broken dreams, it is still a beautiful world.

Be cheerful. Strive to be happy.

Max Ehrmann c.1920
"
        },
        {
            "IhaveaDream", @"Let us not wallow in the valley of despair, I say to you today, my friends.

And so even though we face the difficulties of today and tomorrow, I still have a dream. It is a dream deeply rooted in the American dream.

I have a dream that one day this nation will rise up and live out the true meaning of its creed: 'We hold these truths to be self-evident, that all men are created equal.'

I have a dream that one day on the red hills of Georgia, the sons of former slaves and the sons of former slave owners will be able to sit down together at the table of brotherhood.

I have a dream that one day even the state of Mississippi, a state sweltering with the heat of injustice, sweltering with the heat of oppression, will be transformed into an oasis of freedom and justice.

I have a dream that my four little children will one day live in a nation where they will not be judged by the color of their skin but by the content of their character.

I have a dream today!

I have a dream that one day, down in Alabama, with its vicious racists, with its governor having his lips dripping with the words of 'interposition' and 'nullification' -- one day right there in Alabama little black boys and black girls will be able to join hands with little white boys and white girls as sisters and brothers.

I have a dream today!

I have a dream that one day every valley shall be exalted, and every hill and mountain shall be made low, the rough places will be made plain, and the crooked places will be made straight; 'and the glory of the Lord shall be revealed and all flesh shall see it together.'2
"
        },
        {
            "LoremIpsum",
            "Lorem ipsum dolor sit amet, consectetuer adipiscing elit. Integer " +
"vulputate, nibh non rhoncus euismod, erat odio pellentesque lacus, sit " +
"amet convallis mi augue et odio. Phasellus cursus urna facilisis " +
"quam. Suspendisse nec metus et sapien scelerisque euismod. Nullam " +
"molestie sem quis nisl. Fusce pellentesque, ante sed semper egestas, sem " +
"nulla vestibulum nulla, quis sollicitudin leo lorem elementum " +
"wisi. Aliquam vestibulum nonummy orci. Sed in dolor sed enim ullamcorper " +
"accumsan. Duis vel nibh. Class aptent taciti sociosqu ad litora torquent " +
"per conubia nostra, per inceptos hymenaeos. Sed faucibus, enim sit amet " +
"venenatis laoreet, nisl elit posuere est, ut sollicitudin tortor velit " +
"ut ipsum. Aliquam erat volutpat. Phasellus tincidunt vehicula " +
"eros. Curabitur vitae erat. " +
"\n " +
"Quisque pharetra lacus quis sapien. Duis id est non wisi sagittis " +
"adipiscing. Nulla facilisi. Etiam quam erat, lobortis eu, facilisis nec, " +
"blandit hendrerit, metus. Fusce hendrerit. Nunc magna libero, " +
"sollicitudin non, vulputate non, ornare id, nulla.  Suspendisse " +
"potenti. Nullam in mauris. Curabitur et nisl vel purus vehicula " +
"sodales. Class aptent taciti sociosqu ad litora torquent per conubia " +
"nostra, per inceptos hymenaeos. Cum sociis natoque penatibus et magnis " +
"dis parturient montes, nascetur ridiculus mus. Donec semper, arcu nec " +
"dignissim porta, eros odio tempus pede, et laoreet nibh arcu et " +
"nisl. Morbi pellentesque eleifend ante. Morbi dictum lorem non " +
"ante. Nullam et augue sit amet sapien varius mollis. " +
"\n " +
"Nulla erat lorem, fringilla eget, ultrices nec, dictum sed, " +
"sapien. Aliquam libero ligula, porttitor scelerisque, lobortis nec, " +
"dignissim eu, elit. Etiam feugiat, dui vitae laoreet faucibus, tellus " +
"urna molestie purus, sit amet pretium lorem pede in erat.  Ut non libero " +
"et sapien porttitor eleifend. Vestibulum ante ipsum primis in faucibus " +
"orci luctus et ultrices posuere cubilia Curae; In at lorem et lacus " +
"feugiat iaculis. Nunc tempus eros nec arcu tristique egestas. Quisque " +
"metus arcu, pretium in, suscipit dictum, bibendum sit amet, " +
"mauris. Aliquam non urna. Suspendisse eget diam. Aliquam erat " +
"volutpat. In euismod aliquam lorem. Mauris dolor nisl, consectetuer sit " +
"amet, suscipit sodales, rutrum in, lorem. Nunc nec nisl. Nulla ante " +
"libero, aliquam porttitor, aliquet at, imperdiet sed, diam. Pellentesque " +
"tincidunt nisl et ipsum. Suspendisse purus urna, semper quis, laoreet " +
"in, vestibulum vel, arcu. Nunc elementum eros nec mauris. " +
"\n " +
"Vivamus congue pede at quam. Aliquam aliquam leo vel turpis. Ut " +
"commodo. Integer tincidunt sem a risus. Cras aliquam libero quis " +
"arcu. Integer posuere. Nulla malesuada, wisi ac elementum sollicitudin, " +
"libero libero molestie velit, eu faucibus est ante eu libero. Sed " +
"vestibulum, dolor ac ultricies consectetuer, tellus risus interdum diam, " +
"a imperdiet nibh eros eget mauris. Donec faucibus volutpat " +
"augue. Phasellus vitae arcu quis ipsum ultrices fermentum. Vivamus " +
"ultricies porta ligula. Nullam malesuada. Ut feugiat urna non " +
"turpis. Vivamus ipsum. Vivamus eleifend condimentum risus. Curabitur " +
"pede. Maecenas suscipit pretium tortor. Integer pellentesque. " +
"\n " +
"Mauris est. Aenean accumsan purus vitae ligula. Lorem ipsum dolor sit " +
"amet, consectetuer adipiscing elit. Nullam at mauris id turpis placerat " +
"accumsan. Sed pharetra metus ut ante. Aenean vel urna sit amet ante " +
"pretium dapibus. Sed nulla. Sed nonummy, lacus a suscipit semper, erat " +
"wisi convallis mi, et accumsan magna elit laoreet sem. Nam leo est, " +
"cursus ut, molestie ac, laoreet id, mauris. Suspendisse auctor nibh. " +
        "\n"
        },
    };

    public BZip2InputStreamTests()
    {
        _random = new Random();
    }

    [TestMethod]
    [Timeout(15 * 60 * 1000)] // 60*1000 = 1min
    public void BZBasic()
    {
        // select a random text string
        var line = _testStrings.ElementAt(_random.Next(0, _testStrings.Count)).Value;
        int n = 4000 + _random.Next(1000); // number of iters
        var fname = "Pippo.txt";

        // emit many many lines into a text file:
        using (var sw = new StreamWriter(File.Create(fname)))
        {
            for (int k = 0; k < n; k++)
            {
                sw.WriteLine(line);
            }
        }

        int crcOriginal = GetCrc(fname);
        int blockSize = 0;

        Func<Stream, Stream>[] getBzStream =
        {
            new(s0 =>
            {
                var decorator = new Ionic.BZip2.BZip2OutputStream(s0, blockSize);
                return decorator;
            }),
            new(s1 =>
            {
                    var decorator = new Ionic.BZip2.ParallelBZip2OutputStream(s1, blockSize);
                    return decorator;
            }),
        };

        int[] blockSizes = { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        for (int k = 0; k < getBzStream.Length; k++)
        {
            for (int m = 0; m < blockSizes.Length; m++)
            {
                blockSize = blockSizes[m];
                var getStream = getBzStream[k];
                var root = Path.GetFileNameWithoutExtension(fname);
                var ext = Path.GetExtension(fname);

                // compress into bz2
                var bzFname = string.Format("{0}.{1}.blocksize{2}{3}.bz2", root, (k == 0) ? "SingleThread" : "MultiThread", blockSize, ext);

                using (var fs = File.OpenRead(fname))
                {
                    using var output = File.Create(bzFname);
                    using var compressor = getStream(output);

                    CopyStream(fs, compressor);
                }

                // decompress
                var decompressedFname = Path.GetFileNameWithoutExtension(bzFname);
                using (Stream fs = File.OpenRead(bzFname),
                       output = File.Create(decompressedFname),
                       decompressor = new MpqTool.BZip2.BZip2InputStream(fs))
                {
                    CopyStream(decompressor, output);
                }

                // check crc
                int crcDecompressed = GetCrc(decompressedFname);
                Assert.AreEqual(crcOriginal, crcDecompressed, "CRC mismatch {0:X8} != {1:X8}", crcOriginal, crcDecompressed);

                // just for the sake of disk space economy:
                File.Delete(decompressedFname);
                File.Delete(bzFname);
            }
        }
    }

    [TestMethod]
    [ExpectedException(typeof(IOException))]
    public void BZError1()
    {
        string decompressedFname = "ThisWillNotWork.txt";

        using Stream input = File.OpenRead(Path.Join(Directory.GetCurrentDirectory(), "Heroes.MpqTool.Tests.dll")),
               decompressor = new MpqTool.BZip2.BZip2InputStream(input),
               output = File.Create(decompressedFname);
    }

    [TestMethod]
    [ExpectedException(typeof(IOException))]
    public void BZError2()
    {
        string decompressedFname = "ThisWillNotWork.txt";

        using Stream input = new MemoryStream(), // empty stream
               decompressor = new Ionic.BZip2.BZip2InputStream(input),
               output = File.Create(decompressedFname);
    }

    [TestMethod]
    public void BZSamples()
    {
        var files = Directory.GetFiles("BZip2Files");

        Assert.IsTrue(files.Length > 2, "There are not enough sample files");

        foreach (var filename in files)
        {
            using var fs = File.OpenRead(filename);
            using var decompressor = new Ionic.BZip2.BZip2InputStream(fs);
        }
    }

    private static int GetCrc(string fname)
    {
        using var fs1 = File.OpenRead(fname);
        var checker = new Ionic.Crc.CRC32(true);
        return checker.GetCrc32(fs1);
    }

    private static void CopyStream(Stream src, Stream dest)
    {
        byte[] buffer = new byte[4096];
        int n;
        while ((n = src.Read(buffer, 0, buffer.Length)) > 0)
        {
            dest.Write(buffer, 0, n);
        }
    }
}

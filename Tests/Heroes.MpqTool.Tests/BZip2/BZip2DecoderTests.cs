using Heroes.MpqTool.BZip2;

namespace Heroes.MpqTool.Tests.BZip2;

[TestClass]
public class BZip2DecoderTests
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

    public BZip2DecoderTests()
    {
        _random = new Random();
    }

    [TestMethod]
    public void Decompress_TextCompressedAtEveryBlockSize_ReturnsOriginal()
    {
        // arrange
        byte[] original = CreateText();

        for (int blockSize = 1; blockSize <= 9; blockSize++)
        {
            foreach (bool parallel in new[] { false, true })
            {
                byte[] compressed = Compress(original, blockSize, parallel);
                byte[] decompressed = new byte[original.Length];

                // act
                BZip2Decoder.Decompress(compressed, decompressed);

                // assert
                Assert.IsTrue(original.AsSpan().SequenceEqual(decompressed), $"Mismatch for block size {blockSize}, parallel {parallel}");
            }
        }
    }

    [TestMethod]
    public void Decompress_RandomBytes_ReturnsOriginal()
    {
        // arrange
        // random data gives large move-to-front positions, like the binary data in replays
        byte[] original = new byte[1_500_000];
        new Random(5).NextBytes(original);

        byte[] compressed = Compress(original, 9, false);
        byte[] decompressed = new byte[original.Length];

        // act
        BZip2Decoder.Decompress(compressed, decompressed);

        // assert
        Assert.IsTrue(original.AsSpan().SequenceEqual(decompressed));
    }

    [TestMethod]
    public void Decompress_PeriodicBlock_ReturnsOriginal()
    {
        // arrange
        // A block of a repeating pattern doesn't make one cycle when undoing the Burrows-Wheeler transform.
        // This is "ab" 300000 times, compressed with bzip2 1.0.8 (bzip2 -9), which (unlike Ionic) doesn't randomise the block.
        byte[] compressed = Convert.FromBase64String("QlpoOTFBWSZTWZ/z4CUCSe+BADAAIAAwgCkYFVERgVURHF3JFOFCQn/PgJQ=");
        byte[] original = CreatePeriodicData();
        byte[] decompressed = new byte[original.Length];

        // act
        BZip2Decoder.Decompress(compressed, decompressed);

        // assert
        Assert.IsTrue(original.AsSpan().SequenceEqual(decompressed));
    }

    [TestMethod]
    public void Decompress_RandomisedBlock_ReturnsOriginal()
    {
        // arrange
        // Ionic randomises blocks that are slow to sort, like a repeating pattern
        byte[] original = CreatePeriodicData();
        byte[] compressed = Compress(original, 9, false);
        byte[] decompressed = new byte[original.Length];

        // the randomised flag is the top bit after the 4 byte stream header, 6 byte block header, and 4 byte block CRC
        Assert.AreNotEqual(0, compressed[14] & 0x80, "The block is not randomised");

        // act
        BZip2Decoder.Decompress(compressed, decompressed);

        // assert
        Assert.IsTrue(original.AsSpan().SequenceEqual(decompressed));
    }

    [TestMethod]
    public void Decompress_DestinationSmallerThanData_FillsDestination()
    {
        // arrange
        byte[] original = CreateText();
        byte[] compressed = Compress(original, 1, false);
        byte[] decompressed = new byte[original.Length / 3];

        // act
        BZip2Decoder.Decompress(compressed, decompressed);

        // assert
        Assert.IsTrue(original.AsSpan(0, decompressed.Length).SequenceEqual(decompressed));
    }

    [TestMethod]
    public void Decompress_DestinationLargerThanData_ThrowsException()
    {
        // arrange
        byte[] original = CreateText();
        byte[] compressed = Compress(original, 9, false);
        byte[] decompressed = new byte[original.Length + 1];

        // act / assert
        Assert.ThrowsExactly<IOException>(() => BZip2Decoder.Decompress(compressed, decompressed));
    }

    [TestMethod]
    public void Decompress_TruncatedData_ThrowsException()
    {
        // arrange
        byte[] original = CreateText();
        byte[] compressed = Compress(original, 9, false);
        byte[] decompressed = new byte[original.Length];

        // act / assert
        Assert.ThrowsExactly<IOException>(() => BZip2Decoder.Decompress(compressed.AsSpan(0, compressed.Length / 2), decompressed));
    }

    [TestMethod]
    public void Decompress_WrongBlockCrc_ThrowsException()
    {
        // arrange
        byte[] original = CreateText();
        byte[] compressed = Compress(original, 9, false);
        byte[] decompressed = new byte[original.Length];

        // the first block's CRC follows the 4 byte stream header and 6 byte block header
        compressed[10] ^= 0xFF;

        // act / assert
        Assert.ThrowsExactly<IOException>(() => BZip2Decoder.Decompress(compressed, decompressed));
    }

    [TestMethod]
    public void Decompress_NotBZip2Data_ThrowsException()
    {
        // arrange
        byte[] notBZip2 = File.ReadAllBytes(Path.Join(Directory.GetCurrentDirectory(), "Heroes.MpqTool.Tests.dll"));

        // act / assert
        Assert.ThrowsExactly<IOException>(() => BZip2Decoder.Decompress(notBZip2, new byte[100]));
    }

    [TestMethod]
    public void Decompress_EmptyData_ThrowsException()
    {
        // act / assert
        Assert.ThrowsExactly<IOException>(() => BZip2Decoder.Decompress([], new byte[100]));
    }

    [TestMethod]
    public void Decompress_SampleFiles_MatchesIonicDecompression()
    {
        // arrange
        string[] files = Directory.GetFiles("BZip2Files");

        Assert.IsGreaterThan(2, files.Length, "There are not enough sample files");

        foreach (string filename in files)
        {
            byte[] compressed = File.ReadAllBytes(filename);
            byte[] expected = DecompressWithIonic(compressed);
            byte[] decompressed = new byte[expected.Length];

            // act
            BZip2Decoder.Decompress(compressed, decompressed);

            // assert
            Assert.IsTrue(expected.AsSpan().SequenceEqual(decompressed), $"Mismatch for {filename}");
        }
    }

    private static byte[] Compress(byte[] data, int blockSize, bool parallel)
    {
        using MemoryStream output = new();

        using (Stream compressor = parallel ? new Ionic.BZip2.ParallelBZip2OutputStream(output, blockSize, true) : new Ionic.BZip2.BZip2OutputStream(output, blockSize, true))
        {
            compressor.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] DecompressWithIonic(byte[] compressed)
    {
        using Ionic.BZip2.BZip2InputStream decompressor = new(new MemoryStream(compressed));
        using MemoryStream output = new();

        decompressor.CopyTo(output);

        return output.ToArray();
    }

    // "ab" 300000 times
    private static byte[] CreatePeriodicData()
    {
        byte[] data = new byte[600_000];

        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(i % 2 == 0 ? 'a' : 'b');

        return data;
    }

    // many lines of a random one of the test strings
    private byte[] CreateText()
    {
        string line = _testStrings.ElementAt(_random.Next(0, _testStrings.Count)).Value;
        int count = 4000 + _random.Next(1000);

        using MemoryStream stream = new();

        using (StreamWriter writer = new(stream))
        {
            for (int i = 0; i < count; i++)
                writer.WriteLine(line);
        }

        return stream.ToArray();
    }
}

// Original https://github.com/DinoChiesa/DotNetZip/blob/master/BZip2/BZip2InputStream.cs

namespace Heroes.MpqTool.BZip2;

internal static class BZip2
{
    public static readonly int BlockSizeMultiple = 100000;
    public static readonly int MaxAlphaSize = 258;
    public static readonly int MaxCodeLength = 23;
    public static readonly char RUNA = (char)0;
    public static readonly char RUNB = (char)1;
    public static readonly int NGroups = 6;
    public static readonly int GSize = 50;
    public static readonly int MaxSelectors = 2 + (900000 / GSize);

    internal static T[][] InitRectangularArray<T>(int d1, int d2)
    {
        var x = new T[d1][];
        for (int i = 0; i < d1; i++)
        {
            x[i] = new T[d2];
        }

        return x;
    }
}
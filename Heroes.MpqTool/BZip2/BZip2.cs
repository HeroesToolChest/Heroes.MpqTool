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
}
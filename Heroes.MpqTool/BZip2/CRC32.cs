// CRC32.cs
// Original https://github.com/DinoChiesa/DotNetZip/blob/master/CommonSrc/CRC32.cs

namespace Heroes.MpqTool.BZip2;

/// <summary>
/// Computes a CRC-32. The CRC-32 algorithm is parameterized - you
/// can set the polynomial and enable or disable bit
/// reversal. This can be used for GZIP, BZip2, or ZIP.
/// </summary>
internal class CRC32
{
    private readonly uint _dwPolynomial;
    private readonly bool _reverseBits;
    private readonly uint[] _crc32Table = new uint[256];

    private uint _register = 0xFFFFFFFFU;

    /// <summary>
    ///  Initializes a new instance of the <see cref="CRC32"/> class, specifying whether to reverse data bits or not.
    /// </summary>
    /// <param name='reverseBits'>
    ///  specify true if the instance should reverse data bits.
    /// </param>
    /// <remarks>
    ///   <para>
    ///   In the CRC-32 used by BZip2, the bits are reversed. Therefore if you
    ///   want a CRC32 with compatibility with BZip2, you should pass true
    ///   here. In the CRC-32 used by GZIP and PKZIP, the bits are not
    ///   reversed; Therefore if you want a CRC32 with compatibility with
    ///   those, you should pass false.
    ///   </para>
    /// </remarks>
    public CRC32(bool reverseBits)
        : this(unchecked((int)0xEDB88320), reverseBits)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CRC32"/> class, specifying the polynomial and
    /// whether to reverse data bits or not.
    /// </summary>
    /// <param name='polynomial'>
    /// The polynomial to use for the CRC, expressed in the reversed (LSB)
    /// format: the highest ordered bit in the polynomial value is the
    /// coefficient of the 0th power; the second-highest order bit is the
    /// coefficient of the 1 power, and so on. Expressed this way, the
    /// polynomial for the CRC-32C used in IEEE 802.3, is 0xEDB88320.
    /// </param>
    /// <param name='reverseBits'>
    ///  specify true if the instance should reverse data bits.
    /// </param>
    ///
    /// <remarks>
    ///   <para>
    ///   In the CRC-32 used by BZip2, the bits are reversed. Therefore if you
    ///   want a CRC32 with compatibility with BZip2, you should pass true
    ///   here for the <c>reverseBits</c> parameter. In the CRC-32 used by
    ///   GZIP and PKZIP, the bits are not reversed; Therefore if you want a
    ///   CRC32 with compatibility with those, you should pass false for the
    ///   <c>reverseBits</c> parameter.
    ///   </para>
    /// </remarks>
    public CRC32(int polynomial, bool reverseBits)
    {
        _reverseBits = reverseBits;
        _dwPolynomial = (uint)polynomial;

        GenerateLookupTable();
    }

    /// <summary>
    /// Gets the current CRC for all blocks slurped in.
    /// </summary>
    public int Crc32Result => unchecked((int)~_register);

    /// <summary>
    /// Process one byte in the CRC.
    /// </summary>
    /// <param name = "b">The byte to include into the CRC.</param>
    public void UpdateCRC(byte b)
    {
        if (_reverseBits)
        {
            uint temp = _register >> 24 ^ b;
            _register = _register << 8 ^ _crc32Table[temp];
        }
        else
        {
            uint temp = _register & 0x000000FF ^ b;
            _register = _register >> 8 ^ _crc32Table[temp];
        }
    }

    /// <summary>
    /// Reset the CRC-32 class - clear the CRC "remainder register".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this when employing a single instance of this class to compute
    /// multiple, distinct CRCs on multiple, distinct data blocks.
    /// </para>
    /// </remarks>
    public void Reset()
    {
        _register = 0xFFFFFFFFU;
    }

    private static uint ReverseBits(uint data)
    {
        unchecked
        {
            uint ret = data;
            ret = (ret & 0x55555555) << 1 | ret >> 1 & 0x55555555;
            ret = (ret & 0x33333333) << 2 | ret >> 2 & 0x33333333;
            ret = (ret & 0x0F0F0F0F) << 4 | ret >> 4 & 0x0F0F0F0F;
            ret = ret << 24 | (ret & 0xFF00) << 8 | ret >> 8 & 0xFF00 | ret >> 24;
            return ret;
        }
    }

    private static byte ReverseBits(byte data)
    {
        unchecked
        {
            uint u = (uint)data * 0x00020202;
            uint m = 0x01044010;
            uint s = u & m;
            uint t = u << 2 & m << 1;
            return (byte)((0x01001001 * (s + t)) >> 24);
        }
    }

    private void GenerateLookupTable()
    {
        unchecked
        {
            uint dwCrc;
            byte i = 0;
            do
            {
                dwCrc = i;
                for (byte j = 8; j > 0; j--)
                {
                    if ((dwCrc & 1) == 1)
                    {
                        dwCrc = dwCrc >> 1 ^ _dwPolynomial;
                    }
                    else
                    {
                        dwCrc >>= 1;
                    }
                }

                if (_reverseBits)
                {
                    _crc32Table[ReverseBits(i)] = ReverseBits(dwCrc);
                }
                else
                {
                    _crc32Table[i] = dwCrc;
                }

                i++;
            }
            while (i != 0);
        }
    }
}

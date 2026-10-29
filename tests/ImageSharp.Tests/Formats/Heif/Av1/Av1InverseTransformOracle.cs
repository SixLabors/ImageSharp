// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Runs the non-lossless AV1 inverse transform as the specification text states it in sections 7.13.2 and 7.13.3, and adds the
/// residual to a prediction as the reconstruction process in section 7.12.3 states it.
/// </summary>
/// <remarks>
/// This is the oracle that the inverse transform kernels are measured against, so it is the plainest form of the specification
/// and not fast code. Every value is a 64-bit integer, so no step can overflow. The oracle also records each conformance
/// requirement that the specification states for the transform. A coefficient block that breaks one of them is outside the
/// valid input range, and the caller must not compare a decoder against the result.
/// </remarks>
internal sealed class Av1InverseTransformOracle
{
    /// <summary>
    /// The SINPI_1_9 constant of the inverse ADST4 process.
    /// </summary>
    private const long SinPi19 = 1321;

    /// <summary>
    /// The SINPI_2_9 constant of the inverse ADST4 process.
    /// </summary>
    private const long SinPi29 = 2482;

    /// <summary>
    /// The SINPI_3_9 constant of the inverse ADST4 process.
    /// </summary>
    private const long SinPi39 = 3344;

    /// <summary>
    /// The SINPI_4_9 constant of the inverse ADST4 process.
    /// </summary>
    private const long SinPi49 = 3803;

    /// <summary>
    /// The array T that the one-dimensional processes transform in place.
    /// </summary>
    private readonly long[] t = new long[64];

    /// <summary>
    /// The copy of T that the permutation processes read.
    /// </summary>
    private readonly long[] copyT = new long[64];

    /// <summary>
    /// Whether every conformance requirement held since the start of the current block.
    /// </summary>
    private bool conformant;

    /// <summary>
    /// Gets the Cos128_Lookup table: 4096 * cos(angle * pi / 128) rounded to the nearest integer, for angles 0 to 64.
    /// </summary>
    private static ReadOnlySpan<short> Cos128Lookup =>
    [
        4096, 4095, 4091, 4085, 4076, 4065, 4052, 4036,
        4017, 3996, 3973, 3948, 3920, 3889, 3857, 3822,
        3784, 3745, 3703, 3659, 3612, 3564, 3513, 3461,
        3406, 3349, 3290, 3229, 3166, 3102, 3035, 2967,
        2896, 2824, 2751, 2675, 2598, 2520, 2440, 2359,
        2276, 2191, 2106, 2019, 1931, 1842, 1751, 1660,
        1567, 1474, 1380, 1285, 1189, 1092, 995, 897,
        799, 700, 601, 501, 401, 301, 201, 101, 0
    ];

    /// <summary>
    /// Gets the Transform_Row_Shift table, indexed by the specification transform size.
    /// </summary>
    private static ReadOnlySpan<byte> TransformRowShift => [0, 1, 2, 2, 2, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2];

    /// <summary>
    /// Gets the Tx_Width_Log2 table, indexed by the specification transform size.
    /// </summary>
    private static ReadOnlySpan<byte> TransformWidthLog2 => [2, 3, 4, 5, 6, 2, 3, 3, 4, 4, 5, 5, 6, 2, 4, 3, 5, 4, 6];

    /// <summary>
    /// Gets the Tx_Height_Log2 table, indexed by the specification transform size.
    /// </summary>
    private static ReadOnlySpan<byte> TransformHeightLog2 => [2, 3, 4, 5, 6, 3, 2, 4, 3, 5, 4, 6, 5, 4, 2, 5, 3, 6, 4];

    /// <summary>
    /// Gets the base 2 logarithm of the transform width.
    /// </summary>
    /// <param name="size">The transform size. Its enumeration order is the specification order of TX_4X4 to TX_64X16.</param>
    /// <returns>The value of Tx_Width_Log2.</returns>
    public static int GetWidthLog2(Av1TransformSize size) => TransformWidthLog2[(int)size];

    /// <summary>
    /// Gets the base 2 logarithm of the transform height.
    /// </summary>
    /// <param name="size">The transform size. Its enumeration order is the specification order of TX_4X4 to TX_64X16.</param>
    /// <returns>The value of Tx_Height_Log2.</returns>
    public static int GetHeightLog2(Av1TransformSize size) => TransformHeightLog2[(int)size];

    /// <summary>
    /// Gets the cos128 function of the butterfly rotation.
    /// </summary>
    /// <param name="angle">The angle in units of pi / 128.</param>
    /// <returns>4096 * cos(angle * pi / 128) rounded to the nearest integer.</returns>
    public static long Cos128(int angle)
    {
        // The table covers the first quarter turn. The other quarters reflect it and change the sign.
        int angle2 = angle & 255;
        if (angle2 <= 64)
        {
            return Cos128Lookup[angle2];
        }

        if (angle2 <= 128)
        {
            return -Cos128Lookup[128 - angle2];
        }

        if (angle2 <= 192)
        {
            return -Cos128Lookup[angle2 - 128];
        }

        return Cos128Lookup[256 - angle2];
    }

    /// <summary>
    /// Gets the sin128 function of the butterfly rotation.
    /// </summary>
    /// <param name="angle">The angle in units of pi / 128.</param>
    /// <returns>4096 * sin(angle * pi / 128) rounded to the nearest integer.</returns>
    public static long Sin128(int angle) => Cos128(angle - 64);

    /// <summary>
    /// Adds a residual block to its prediction as step 3 of the reconstruction process in section 7.12.3.
    /// </summary>
    /// <param name="residual">The residual from <see cref="TryInverseTransform2d"/>, Residual[ i ][ j ] at i * w + j.</param>
    /// <param name="type">The transform type, which selects the flips.</param>
    /// <param name="size">The transform size.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">Receives the reconstructed samples.</param>
    /// <param name="destinationStride">The number of destination samples between rows.</param>
    public static void Reconstruct(
        ReadOnlySpan<long> residual,
        Av1TransformType type,
        Av1TransformSize size,
        int bitDepth,
        ReadOnlySpan<int> prediction,
        int predictionStride,
        Span<int> destination,
        int destinationStride)
    {
        int w = 1 << GetWidthLog2(size);
        int h = 1 << GetHeightLog2(size);
        bool flipUD = type is Av1TransformType.FlipAdstDct or Av1TransformType.FlipAdstAdst or Av1TransformType.VerticalFlipAdst or
            Av1TransformType.FlipAdstFlipAdst;

        bool flipLR = type is Av1TransformType.DctFlipAdst or Av1TransformType.AdstFlipAdst or Av1TransformType.HorizontalFlipAdst or
            Av1TransformType.FlipAdstFlipAdst;

        // A flipped axis writes residual row or column k to the mirrored sample position. Clip1 keeps each sample in range.
        long maximum = (1L << bitDepth) - 1;
        for (int i = 0; i < h; i++)
        {
            for (int j = 0; j < w; j++)
            {
                int xx = flipLR ? w - j - 1 : j;
                int yy = flipUD ? h - i - 1 : i;
                long sample = prediction[(yy * predictionStride) + xx] + residual[(i * w) + j];
                destination[(yy * destinationStride) + xx] = (int)Math.Clamp(sample, 0, maximum);
            }
        }
    }

    /// <summary>
    /// Runs the 2D inverse transform process for one block that is not lossless.
    /// </summary>
    /// <param name="dequant">
    /// The dequantized coefficients: Dequant[ i ][ j ] is at i * Min(32, w) + j, for the first Min(32, h) rows and Min(32, w) columns.
    /// </param>
    /// <param name="type">The transform type, PlaneTxType.</param>
    /// <param name="size">The transform size, txSz.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="residual">Receives Residual[ i ][ j ] at i * w + j.</param>
    /// <returns>
    /// <see langword="true"/> when every conformance requirement of the transform held; otherwise, <see langword="false"/>, and the
    /// coefficients are outside the valid input range.
    /// </returns>
    public bool TryInverseTransform2d(ReadOnlySpan<int> dequant, Av1TransformType type, Av1TransformSize size, int bitDepth, Span<long> residual)
    {
        int log2W = GetWidthLog2(size);
        int log2H = GetHeightLog2(size);
        int w = 1 << log2W;
        int h = 1 << log2H;
        int tw = Math.Min(32, w);
        int rowShift = TransformRowShift[(int)size];
        const int colShift = 4;
        int rowClampRange = bitDepth + 8;
        int colClampRange = Math.Max(bitDepth + 6, 16);
        long[] t = this.t;
        this.conformant = true;

        // The row transforms. Only the top-left 32x32 coefficients can be nonzero.
        for (int i = 0; i < h; i++)
        {
            for (int j = 0; j < w; j++)
            {
                t[j] = i < 32 && j < 32 ? dequant[(i * tw) + j] : 0;
            }

            // A 2:1 or 1:2 block scales its row inputs by 1 / sqrt(2).
            if (Math.Abs(log2W - log2H) == 1)
            {
                for (int j = 0; j < w; j++)
                {
                    t[j] = Round2(t[j] * 2896, 12);
                }
            }

            if (IsRowDct(type))
            {
                this.InverseDct(log2W, rowClampRange);
            }
            else if (IsRowAdst(type))
            {
                this.InverseAdst(log2W, rowClampRange);
            }
            else
            {
                this.InverseIdentity(log2W);
            }

            for (int j = 0; j < w; j++)
            {
                residual[(i * w) + j] = Round2(t[j], rowShift);
            }
        }

        // The column inputs are clamped once, between the two passes.
        long colMin = -(1L << (colClampRange - 1));
        long colMax = (1L << (colClampRange - 1)) - 1;
        for (int k = 0; k < w * h; k++)
        {
            residual[k] = Math.Clamp(residual[k], colMin, colMax);
        }

        // The column transforms.
        for (int j = 0; j < w; j++)
        {
            for (int i = 0; i < h; i++)
            {
                t[i] = residual[(i * w) + j];
            }

            if (IsColumnDct(type))
            {
                this.InverseDct(log2H, colClampRange);
            }
            else if (IsColumnAdst(type))
            {
                this.InverseAdst(log2H, colClampRange);
            }
            else
            {
                this.InverseIdentity(log2H);
            }

            for (int i = 0; i < h; i++)
            {
                residual[(i * w) + j] = Round2(t[i], colShift);
            }
        }

        // Section 7.12.3 notes that the other constraints keep every residual of a valid block inside Max(BitDepth + 5, 15) signed
        // bits. A block outside that range is not valid input either.
        int residualBits = Math.Max(bitDepth + 5, 15);
        for (int k = 0; k < w * h; k++)
        {
            this.RequireRepresentable(residual[k], residualBits);
        }

        return this.conformant;
    }

    /// <summary>
    /// The transform types whose row transform is the inverse DCT.
    /// </summary>
    /// <param name="type">The transform type.</param>
    /// <returns><see langword="true"/> when the row transform is the inverse DCT.</returns>
    private static bool IsRowDct(Av1TransformType type)
        => type is Av1TransformType.DctDct or Av1TransformType.AdstDct or Av1TransformType.FlipAdstDct or Av1TransformType.HorizontalDct;

    /// <summary>
    /// The transform types whose row transform is the inverse ADST.
    /// </summary>
    /// <param name="type">The transform type.</param>
    /// <returns><see langword="true"/> when the row transform is the inverse ADST.</returns>
    private static bool IsRowAdst(Av1TransformType type)
        => type is Av1TransformType.DctAdst or Av1TransformType.AdstAdst or Av1TransformType.DctFlipAdst or Av1TransformType.FlipAdstFlipAdst or
            Av1TransformType.AdstFlipAdst or Av1TransformType.FlipAdstAdst or Av1TransformType.HorizontalAdst or Av1TransformType.HorizontalFlipAdst;

    /// <summary>
    /// The transform types whose column transform is the inverse DCT.
    /// </summary>
    /// <param name="type">The transform type.</param>
    /// <returns><see langword="true"/> when the column transform is the inverse DCT.</returns>
    private static bool IsColumnDct(Av1TransformType type)
        => type is Av1TransformType.DctDct or Av1TransformType.DctAdst or Av1TransformType.DctFlipAdst or Av1TransformType.VerticalDct;

    /// <summary>
    /// The transform types whose column transform is the inverse ADST.
    /// </summary>
    /// <param name="type">The transform type.</param>
    /// <returns><see langword="true"/> when the column transform is the inverse ADST.</returns>
    private static bool IsColumnAdst(Av1TransformType type)
        => type is Av1TransformType.AdstDct or Av1TransformType.AdstAdst or Av1TransformType.FlipAdstDct or Av1TransformType.FlipAdstFlipAdst or
            Av1TransformType.AdstFlipAdst or Av1TransformType.FlipAdstAdst or Av1TransformType.VerticalAdst or Av1TransformType.VerticalFlipAdst;

    /// <summary>
    /// Computes Round2 as the specification defines it, with an arithmetic right shift.
    /// </summary>
    /// <param name="x">The value.</param>
    /// <param name="n">The number of bits to remove.</param>
    /// <returns>The rounded value.</returns>
    private static long Round2(long x, int n) => n == 0 ? x : (x + (1L << (n - 1))) >> n;

    /// <summary>
    /// Computes the brev function: the bit reversal of the low bits of a value.
    /// </summary>
    /// <param name="numBits">The number of bits to reverse.</param>
    /// <param name="x">The value.</param>
    /// <returns>The reversed bits.</returns>
    private static int Brev(int numBits, int x)
    {
        int result = 0;
        for (int i = 0; i < numBits; i++)
        {
            int bit = (x >> i) & 1;
            result += bit << (numBits - 1 - i);
        }

        return result;
    }

    /// <summary>
    /// Records a conformance failure when a value is not representable by a signed integer with the given number of bits.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="bits">The number of signed bits.</param>
    private void RequireRepresentable(long value, int bits)
    {
        if (value < -(1L << (bits - 1)) || value > (1L << (bits - 1)) - 1)
        {
            this.conformant = false;
        }
    }

    /// <summary>
    /// Runs the butterfly rotation B( a, b, angle, flip, r ).
    /// </summary>
    /// <param name="a">The first index into T.</param>
    /// <param name="b">The second index into T.</param>
    /// <param name="angle">The rotation angle in units of pi / 128.</param>
    /// <param name="flip">Whether T[ a ] and T[ b ] are exchanged after the rotation.</param>
    /// <param name="r">The intermediate range. Both results must be representable by r signed bits.</param>
    private void B(int a, int b, int angle, bool flip, int r)
    {
        long[] t = this.t;
        long x = (t[a] * Cos128(angle)) - (t[b] * Sin128(angle));
        long y = (t[a] * Sin128(angle)) + (t[b] * Cos128(angle));
        t[a] = Round2(x, 12);
        t[b] = Round2(y, 12);
        this.RequireRepresentable(t[a], r);
        this.RequireRepresentable(t[b], r);

        if (flip)
        {
            (t[a], t[b]) = (t[b], t[a]);
        }
    }

    /// <summary>
    /// Runs the Hadamard rotation H( a, b, flip, r ), which clamps both results to r signed bits.
    /// </summary>
    /// <param name="a">The first index into T.</param>
    /// <param name="b">The second index into T.</param>
    /// <param name="flip">Whether the indices are exchanged before the rotation.</param>
    /// <param name="r">The intermediate clamping range.</param>
    private void H(int a, int b, int flip, int r)
    {
        if (flip != 0)
        {
            (a, b) = (b, a);
        }

        long[] t = this.t;
        long x = t[a];
        long y = t[b];
        long minimum = -(1L << (r - 1));
        long maximum = (1L << (r - 1)) - 1;
        t[a] = Math.Clamp(x + y, minimum, maximum);
        t[b] = Math.Clamp(x - y, minimum, maximum);
    }

    /// <summary>
    /// Runs the inverse DCT array permutation process: T[ i ] takes copyT[ brev( n, i ) ].
    /// </summary>
    /// <param name="n">The base 2 logarithm of the array length.</param>
    private void InverseDctPermutation(int n)
    {
        int n0 = 1 << n;
        Array.Copy(this.t, this.copyT, n0);
        for (int i = 0; i < n0; i++)
        {
            this.t[i] = this.copyT[Brev(n, i)];
        }
    }

    /// <summary>
    /// Runs the inverse DCT process on T, as the ordered steps of section 7.13.2.3.
    /// </summary>
    /// <param name="n">The base 2 logarithm of the array length, 2 to 6.</param>
    /// <param name="r">The intermediate clamping range.</param>
    private void InverseDct(int n, int r)
    {
        // Step 1.
        this.InverseDctPermutation(n);

        // Steps 2 to 7.
        if (n == 6)
        {
            for (int i = 0; i < 16; i++)
            {
                this.B(32 + i, 63 - i, 63 - (4 * Brev(4, i)), false, r);
            }
        }

        if (n >= 5)
        {
            for (int i = 0; i < 8; i++)
            {
                this.B(16 + i, 31 - i, 6 + (Brev(3, 7 - i) << 3), false, r);
            }
        }

        if (n == 6)
        {
            for (int i = 0; i < 16; i++)
            {
                this.H(32 + (i * 2), 33 + (i * 2), i & 1, r);
            }
        }

        if (n >= 4)
        {
            for (int i = 0; i < 4; i++)
            {
                this.B(8 + i, 15 - i, 12 + (Brev(2, 3 - i) << 4), false, r);
            }
        }

        if (n >= 5)
        {
            for (int i = 0; i < 8; i++)
            {
                this.H(16 + (2 * i), 17 + (2 * i), i & 1, r);
            }
        }

        if (n == 6)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    this.B(62 - (i * 4) - j, 33 + (i * 4) + j, 60 - (16 * Brev(2, i)) + (64 * j), true, r);
                }
            }
        }

        // Steps 8 to 11.
        if (n >= 3)
        {
            for (int i = 0; i < 2; i++)
            {
                this.B(4 + i, 7 - i, 56 - (32 * i), false, r);
            }
        }

        if (n >= 4)
        {
            for (int i = 0; i < 4; i++)
            {
                this.H(8 + (2 * i), 9 + (2 * i), i & 1, r);
            }
        }

        if (n >= 5)
        {
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    this.B(30 - (4 * i) - j, 17 + (4 * i) + j, 24 + (j << 6) + ((1 - i) << 5), true, r);
                }
            }
        }

        if (n == 6)
        {
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    this.H(32 + (i * 4) + j, 35 + (i * 4) - j, i & 1, r);
                }
            }
        }

        // Steps 12 to 16.
        for (int i = 0; i < 2; i++)
        {
            this.B(2 * i, (2 * i) + 1, 32 + (16 * i), i == 0, r);
        }

        if (n >= 3)
        {
            for (int i = 0; i < 2; i++)
            {
                this.H(4 + (2 * i), 5 + (2 * i), i, r);
            }
        }

        if (n >= 4)
        {
            for (int i = 0; i < 2; i++)
            {
                this.B(14 - i, 9 + i, 48 + (64 * i), true, r);
            }
        }

        if (n >= 5)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    this.H(16 + (4 * i) + j, 19 + (4 * i) - j, i & 1, r);
                }
            }
        }

        if (n == 6)
        {
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    this.B(61 - (i * 8) - j, 34 + (i * 8) + j, 56 - (i * 32) + ((j >> 1) * 64), true, r);
                }
            }
        }

        // Steps 17 to 21.
        for (int i = 0; i < 2; i++)
        {
            this.H(i, 3 - i, 0, r);
        }

        if (n >= 3)
        {
            this.B(6, 5, 32, true, r);
        }

        if (n >= 4)
        {
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    this.H(8 + (4 * i) + j, 11 + (4 * i) - j, i, r);
                }
            }
        }

        if (n >= 5)
        {
            for (int i = 0; i < 4; i++)
            {
                this.B(29 - i, 18 + i, 48 + ((i >> 1) * 64), true, r);
            }
        }

        if (n == 6)
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    this.H(32 + (8 * i) + j, 39 + (8 * i) - j, i & 1, r);
                }
            }
        }

        // Steps 22 to 25.
        if (n >= 3)
        {
            for (int i = 0; i < 4; i++)
            {
                this.H(i, 7 - i, 0, r);
            }
        }

        if (n >= 4)
        {
            for (int i = 0; i < 2; i++)
            {
                this.B(13 - i, 10 + i, 32, true, r);
            }
        }

        if (n >= 5)
        {
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    this.H(16 + (i * 8) + j, 23 + (i * 8) - j, i, r);
                }
            }
        }

        if (n == 6)
        {
            for (int i = 0; i < 8; i++)
            {
                this.B(59 - i, 36 + i, i < 4 ? 48 : 112, true, r);
            }
        }

        // Steps 26 to 31.
        if (n >= 4)
        {
            for (int i = 0; i < 8; i++)
            {
                this.H(i, 15 - i, 0, r);
            }
        }

        if (n >= 5)
        {
            for (int i = 0; i < 4; i++)
            {
                this.B(27 - i, 20 + i, 32, true, r);
            }
        }

        if (n == 6)
        {
            for (int i = 0; i < 8; i++)
            {
                this.H(32 + i, 47 - i, 0, r);
                this.H(48 + i, 63 - i, 1, r);
            }
        }

        if (n >= 5)
        {
            for (int i = 0; i < 16; i++)
            {
                this.H(i, 31 - i, 0, r);
            }
        }

        if (n == 6)
        {
            for (int i = 0; i < 8; i++)
            {
                this.B(55 - i, 40 + i, 32, true, r);
            }

            for (int i = 0; i < 32; i++)
            {
                this.H(i, 63 - i, 0, r);
            }
        }
    }

    /// <summary>
    /// Runs the inverse ADST input array permutation process.
    /// </summary>
    /// <param name="n">The base 2 logarithm of the array length, 3 or 4.</param>
    private void InverseAdstInputPermutation(int n)
    {
        int n0 = 1 << n;
        Array.Copy(this.t, this.copyT, n0);
        for (int i = 0; i < n0; i++)
        {
            int idx = (i & 1) != 0 ? i - 1 : n0 - i - 1;
            this.t[i] = this.copyT[idx];
        }
    }

    /// <summary>
    /// Runs the inverse ADST output array permutation process, which also negates the odd outputs.
    /// </summary>
    /// <param name="n">The base 2 logarithm of the array length, 3 or 4.</param>
    private void InverseAdstOutputPermutation(int n)
    {
        int n0 = 1 << n;
        Array.Copy(this.t, this.copyT, n0);
        for (int i = 0; i < n0; i++)
        {
            int a = (i >> 3) & 1;
            int b = ((i >> 2) & 1) ^ ((i >> 3) & 1);
            int c = ((i >> 1) & 1) ^ ((i >> 2) & 1);
            int d = (i & 1) ^ ((i >> 1) & 1);
            int idx = ((d << 3) | (c << 2) | (b << 1) | a) >> (4 - n);
            this.t[i] = (i & 1) != 0 ? -this.copyT[idx] : this.copyT[idx];
        }
    }

    /// <summary>
    /// Runs the inverse ADST process on T.
    /// </summary>
    /// <param name="n">The base 2 logarithm of the array length, 2 to 4.</param>
    /// <param name="r">The intermediate clamping range.</param>
    private void InverseAdst(int n, int r)
    {
        if (n == 2)
        {
            this.InverseAdst4(r);
        }
        else if (n == 3)
        {
            this.InverseAdst8(r);
        }
        else
        {
            this.InverseAdst16(r);
        }
    }

    /// <summary>
    /// Runs the inverse ADST4 process on T.
    /// </summary>
    /// <param name="r">The intermediate range that sets the conformance limits of the s, x, a7, and b7 values.</param>
    private void InverseAdst4(int r)
    {
        long[] t = this.t;
        Span<long> s = stackalloc long[7];
        Span<long> x = stackalloc long[4];

        s[0] = SinPi19 * t[0];
        s[1] = SinPi29 * t[0];
        s[2] = SinPi39 * t[1];
        s[3] = SinPi49 * t[2];
        s[4] = SinPi19 * t[2];
        s[5] = SinPi29 * t[3];
        s[6] = SinPi49 * t[3];
        this.RequireAllRepresentable(s, r + 12);

        long a7 = t[0] - t[2];
        long b7 = a7 + t[3];
        this.RequireRepresentable(a7, r + 1);
        this.RequireRepresentable(b7, r);

        s[0] += s[3];
        s[1] -= s[4];
        s[3] = s[2];
        s[2] = SinPi39 * b7;
        this.RequireAllRepresentable(s, r + 12);

        s[0] += s[5];
        s[1] -= s[6];
        this.RequireAllRepresentable(s, r + 12);

        x[0] = s[0] + s[3];
        x[1] = s[1] + s[3];
        x[2] = s[2];
        x[3] = s[0] + s[1];
        this.RequireAllRepresentable(x, r + 12);

        x[3] -= s[3];
        this.RequireAllRepresentable(x, r + 12);

        t[0] = Round2(x[0], 12);
        t[1] = Round2(x[1], 12);
        t[2] = Round2(x[2], 12);
        t[3] = Round2(x[3], 12);
    }

    /// <summary>
    /// Records a conformance failure when any value is not representable by a signed integer with the given number of bits.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <param name="bits">The number of signed bits.</param>
    private void RequireAllRepresentable(ReadOnlySpan<long> values, int bits)
    {
        foreach (long value in values)
        {
            this.RequireRepresentable(value, bits);
        }
    }

    /// <summary>
    /// Runs the inverse ADST8 process on T.
    /// </summary>
    /// <param name="r">The intermediate clamping range.</param>
    private void InverseAdst8(int r)
    {
        this.InverseAdstInputPermutation(3);
        for (int i = 0; i < 4; i++)
        {
            this.B(2 * i, (2 * i) + 1, 60 - (16 * i), true, r);
        }

        for (int i = 0; i < 4; i++)
        {
            this.H(i, 4 + i, 0, r);
        }

        for (int i = 0; i < 2; i++)
        {
            this.B(4 + (3 * i), 5 + i, 48 - (32 * i), true, r);
        }

        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                this.H((4 * j) + i, 2 + (4 * j) + i, 0, r);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            this.B(2 + (4 * i), 3 + (4 * i), 32, true, r);
        }

        this.InverseAdstOutputPermutation(3);
    }

    /// <summary>
    /// Runs the inverse ADST16 process on T.
    /// </summary>
    /// <param name="r">The intermediate clamping range.</param>
    private void InverseAdst16(int r)
    {
        this.InverseAdstInputPermutation(4);
        for (int i = 0; i < 8; i++)
        {
            this.B(2 * i, (2 * i) + 1, 62 - (8 * i), true, r);
        }

        for (int i = 0; i < 8; i++)
        {
            this.H(i, 8 + i, 0, r);
        }

        for (int i = 0; i < 2; i++)
        {
            this.B(8 + (2 * i), 9 + (2 * i), 56 - (32 * i), true, r);
            this.B(13 + (2 * i), 12 + (2 * i), 8 + (32 * i), true, r);
        }

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                this.H((8 * j) + i, 4 + (8 * j) + i, 0, r);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                this.B(4 + (8 * j) + (3 * i), 5 + (8 * j) + i, 48 - (32 * i), true, r);
            }
        }

        for (int i = 0; i < 2; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                this.H((4 * j) + i, 2 + (4 * j) + i, 0, r);
            }
        }

        for (int i = 0; i < 4; i++)
        {
            this.B(2 + (4 * i), 3 + (4 * i), 32, true, r);
        }

        this.InverseAdstOutputPermutation(4);
    }

    /// <summary>
    /// Runs the inverse identity transform process on T.
    /// </summary>
    /// <param name="n">The base 2 logarithm of the array length, 2 to 5.</param>
    private void InverseIdentity(int n)
    {
        long[] t = this.t;
        int n0 = 1 << n;
        for (int i = 0; i < n0; i++)
        {
            t[i] = n switch
            {
                2 => Round2(t[i] * 5793, 12),
                3 => t[i] * 2,
                4 => Round2(t[i] * 11586, 12),
                _ => t[i] * 4,
            };
        }
    }
}

// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

// Suppress IDE0057. This is so we can stack-allocate
// a powers of 2 and then slice it to the appropriate
// length (which produces better code).
//
// Without this suppression, the analyzer produces a warning,
// recommending changing this:
//    stackalloc ulong[32].Slice(0, 18)
// to:
//    (stackalloc ulong[32])[..18]
//
// But then the analyzer produces a new warning, recommending
// to remove the paranthesis, changing this:
//    (stackalloc ulong[32])[..18]
// to:
//    stackalloc ulong[32][..18]
//
// which is invalid C# syntax.
#pragma warning disable IDE0057 // Use range operator

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless;

/// <summary>
/// Extreme performance JPEG XL encoder which provides minimal lossless compression.
/// It also uses minimal dependencies.
/// </summary>
internal sealed class JxlFastLosslessEncoder
{
    /// <summary>
    /// Specifies maximum number of bytes a frame header may use.
    /// </summary>
    public const int MaxFrameHeaderSize = 5;

    public const int NumRawSymbols = 19;

    public const int NumLz77 = 33;

    /// <summary>
    /// Cache/dictionary size for LZ77
    /// </summary>
    public const int Lz77CacheSize = 32;

    public const int Lz77Offset = 224;

    public const int Lz77MinLength = 7;

    private static readonly int LogChunkSize =
        JxlSimdTarget.Using512BitVectors
            ? 5
            : JxlSimdTarget.Using256BitVectors
                ? 4
                : 3;

    /// <summary>
    /// Input frame data is stored here.
    /// </summary>
    private readonly FjxlFrameInputSource input;

    /// <summary>
    /// Image width of the input image.
    /// </summary>
    private readonly int width;

    /// <summary>
    /// Image height of the input image.
    /// </summary>
    private readonly int height;

    /// <summary>
    /// Image width in groups.
    /// </summary>
    private readonly int numGroupsX;

    /// <summary>
    /// Image height in groups.
    /// </summary>
    private readonly int numGroupsY;

    /// <summary>
    /// Image width in groups (DC).
    /// </summary>
    private readonly int numDcGroupsX;

    /// <summary>
    /// Image height in groups (DC).
    /// </summary>
    private readonly int numDcGroupsY;

    /// <summary>
    /// Number of channels. (f.e. RGBA is 4, YUV is 3)
    /// </summary>
    private readonly int channels;

    /// <summary>
    /// Number of bits represented per pixel. (f.e. 8 means pixels
    /// have a 0-255 range)
    /// </summary>
    /// <remarks>
    /// Higher bit depths can represent more colors.
    /// </remarks>
    private readonly int bitDepth;

    /// <summary>
    /// Should the output image be stored in big-endian order?
    /// </summary>
    private readonly bool isBigEndian;

    private readonly int effort;

    private readonly bool collided;

    /// <summary>
    /// Prefix codes for LZ77.
    /// </summary>
    private InlineArray4<FjxlPrefixCode> hcode;

    private readonly List<short> lookup = [];

    /// <summary>
    /// Bit writer to write the JPEG XL headers.
    /// </summary>
    private readonly FjxlBitWriter header;

    /// <summary>
    /// Bit writers for writing JPEG XL groups.
    /// </summary>
    private readonly List<InlineArray4<FjxlBitWriter>> groupData = [];

    /// <summary>
    /// Sizes for each group.
    /// </summary>
    private readonly List<int> groupSizes = [];

    private int acGroupDataOffset;

    private int minDcGlobalSize;

    private int currentBitWriter;

    private int bitWriterBytePos;

    private int bitsInBuffer;

    private long bitBuffer;

    private bool processDone;

    /// <summary>
    /// Abstracts access to a raster frame data required for encoding.
    /// </summary>
    internal abstract class FjxlFrameInputSource : IDisposable
    {
        /// <inheritdoc />
        public abstract void Dispose();

        /// <summary>
        /// Returns a span that wraps over channel color data at the
        /// specified rectangular position.
        /// </summary>
        /// <typeparam name="T">Target type of the color data.</typeparam>
        /// <param name="x">Left offset</param>
        /// <param name="y">Right offset</param>
        /// <param name="width">Selection width</param>
        /// <param name="height">Selection height</param>
        /// <param name="rowOffset">The actual offset of the row in row-major order is stored here.</param>
        /// <returns>
        /// A wrapper over the color data of the channel at the specified
        /// position.
        /// </returns>
        public abstract Span<T> GetColorChannelData<T>(int x, int y, int width, int height, out long rowOffset)
            where T : unmanaged;
    }

    /// <summary>
    /// Gets minimum raw lengths for prefix coding.
    /// </summary>
    public static ReadOnlySpan<byte> MinimumRawLength => [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// Gets maximum raw lengths for prefix coding.
    /// </summary>
    public static ReadOnlySpan<byte> MaximumRawLength => [7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 10];

    /// <summary>
    /// Gets a lookup used by the <see cref="TocBucket"/> method
    /// to translate a bucket into a base group size.
    /// </summary>
    private static ReadOnlySpan<int> GroupSizeOffset =>
    [
        0,
        1024,
        17408,
        4211712
    ];

    /// <summary>
    /// Gets a lookup to determine how many bits a TOC bucket uses.
    /// </summary>
    private static ReadOnlySpan<int> TocBits => [12, 16, 24, 32];

    /// <summary>
    /// Approximates Floor(Log2(v)) using integers.
    /// </summary>
    /// <param name="v">Value to retrieve Floor(Log2(v)) of.</param>
    /// <returns>Floor of second logarithm of v, or 31 if v is equal to 0.</returns>
    /// <remarks>This method may use CPU intrinsics provided by the .NET Runtime (e.g. BMI1 on x86).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FloorLog2(uint v) => v == 0 ? 0 : 31u - (uint)BitOperations.LeadingZeroCount(v);

    /// <summary>
    /// Approximates count trailing zeros of v using integers.
    /// </summary>
    /// <param name="v">Value to retrieve number of 0 bits after last 1 bit of.</param>
    /// <returns>After the least significant 1 bit, returns the number of 0 bits. E.g. 1000 1000 00 -&gt; 5.</returns>
    /// <remarks>This method may use CPU intrinsics provided by the .NET Runtime (e.g. BMI1 on x86).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CtzNonZero(ulong v) => (uint)BitOperations.TrailingZeroCount(v);

    /// <summary>
    /// Returns a TOC bucket based on the group size.
    /// </summary>
    /// <param name="groupSize">Specified group size.</param>
    /// <returns>TOC bucket matching the appropriate group size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TocBucket(int groupSize)
    {
        int bucket = 0;

        while (bucket < 3 && groupSize >= GroupSizeOffset[bucket + 1])
        {
            bucket++;
        }

        return bucket;
    }

    /// <summary>
    /// Returns the total number of bits required to represent
    /// all given group sizes in the TOC.
    /// </summary>
    /// <param name="groupSizes">Group sizes to calculate bit sizes of.</param>
    /// <returns>Accumulated number of bits required to represent each group size.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TocSize(Span<int> groupSizes)
    {
        int tocBits = 0;

        ref int unsafeRef = ref MemoryMarshal.GetReference(groupSizes);

        for (int i = 0; i < groupSizes.Length; i++)
        {
            // TODO: we can try using AVX2 gather intrinsics,
            // especially because TocBits can absolutely fit
            // in the L1 cache
            int groupSize = Unsafe.Add(ref unsafeRef, i);
            int bucketForGroupSize = TocBucket(groupSize);
            int bitsUsedByBucket = TocBits[bucketForGroupSize];

            tocBits += bitsUsedByBucket;
        }

        return tocBits;
    }

    /// <summary>
    /// Returns the number of bytes for the frame header.
    /// </summary>
    /// <param name="containsAlpha">Indicates presence of the alpha channel.</param>
    /// <param name="isLast">Indicates whether this is the final frame.</param>
    /// <returns>Frame header size in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FrameHeaderSize(bool containsAlpha, bool isLast)
    {
        // Original code (from libjxl):
        //
        //     size_t nbits = 28 + (have_alpha ? 4 : 0) + (is_last ? 0 : 2);
        //     return (nbits + 7) / 8;
        //
        // In this implementation we just use constants to shave a few CPU cycles.
        // The total amount of branches is reduced by one (for the !containsAlpha case),
        // but we remove the arithmetic/shifting instructions.
        unchecked
        {
            if (containsAlpha)
            {
                if (isLast)
                {
                    return 5; // (34 + 7) / 8
                }
                else
                {
                    return 4; // (32 + 7) / 8
                }
            }
            else
            {
                return 4; // (30 + 7) / 8 AND (28 + 7) / 8 yield the same result
            }
        }
    }

    private static long GetSectionSize(InlineArray4<FjxlBitWriter> groupData)
    {
        long size = 0;

        for (int j = 0; j < 4; j++)
        {
            FjxlBitWriter writer = groupData[j];

            size += (writer.BytesWritten * 8) + writer.BitsInBuffer;
        }

        return (size + 7) / 8;
    }

    /// <summary>
    /// Approximates number of bytes needed for the output image buffer.
    /// </summary>
    /// <returns>Bytes for the frame buffer.</returns>
    private long GetOutputSize()
    {
        long totalSizeGroups = 0;

        Span<InlineArray4<FjxlBitWriter>> groups = CollectionsMarshal.AsSpan(this.groupData);

        for (int i = 0; i < groups.Length; i++)
        {
            InlineArray4<FjxlBitWriter> section = groups[i];

            totalSizeGroups += GetSectionSize(section);
        }

        return this.header.BytesWritten + totalSizeGroups;
    }

    /// <summary>
    /// Returns the maximum amount of bytes potentially required for the image buffer.
    /// </summary>
    /// <returns>Upper bound of bytes for frame buffer.</returns>
    private long GetMaxRequiredOutput() => this.GetOutputSize() + 32;

    private void WriteHeader(bool addImageHeader, bool isLast)
    {
        FjxlBitWriter output = this.header;
        bool haveAlpha = this.channels is 2 or 4;

        if (addImageHeader)
        {
            // File signature. This signature specifies
            // a raw codestream. No container format here.
            output.Write(16, 0x0AFF);

            // Handcrafted size header.
            output.Write(1, 0); // Not small

            WriteSize(this.height);
            output.Write(3, 0b000); // No special ratio
            WriteSize(this.width);

            // Handcrafted image metadata
            output.Write(1, 0); // all_default = 0 (don't assume values to be set to their defaults)
            output.Write(1, 0); // extra_fields = 0 (extra fields are disabled and therefore not present)
            output.Write(1, 0); // bit_depth.floating_point_sample = 0 (samples are integers)

            if (this.bitDepth == 8)
            {
                output.Write(2, 0b00); // bit_depth.bits_per_sample = 8 (predefined bit depth of 8 bits)
            }
            else if (this.bitDepth == 10)
            {
                output.Write(2, 0b01); // bit_depth.bits_per_sample = 10 (predefined bit depth of 10 bits)
            }
            else if (this.bitDepth == 12)
            {
                output.Write(2, 0b10); // bit_depth.bits_per_sample = 12 (predefined bit depth of 12 bits)
            }
            else
            {
                output.Write(2, 0b11); // Custom bit depth
                output.Write(6, (ulong)this.bitDepth - 1); // bit depth minus 1 (so 0 becomes 1, 9 becomes 10, etc)
            }

            if (this.bitDepth <= 14)
            {
                output.Write(1, 1); // 16-bit-buffer is sufficient
            }
            else
            {
                output.Write(1, 0); // 16-bit-buffer is NOT sufficient
            }

            if (haveAlpha)
            {
                output.Write(2, 0b01); // Emit one extra channel (the alpha channel)

                if (this.bitDepth == 8)
                {
                    output.Write(1, 1); // all_default = 1 (8-bit alpha is the default)
                }
                else
                {
                    output.Write(1, 0); // all_default = 0
                    output.Write(2, 0); // type = alpha
                    output.Write(1, 0); // samples are not floating point

                    if (this.bitDepth == 10)
                    {
                        output.Write(2, 0b01); // bit_depth.bits_per_sample = 10 (predefined bit depth of 10 bits)
                    }
                    else if (this.bitDepth == 12)
                    {
                        output.Write(2, 0b10); // bit_depth.bits_per_sample = 12 (predefined bit depth of 12 bits)
                    }
                    else
                    {
                        output.Write(2, 0b11); // Custom bit depth
                        output.Write(6, (ulong)this.bitDepth - 1); // bit depth minus 1 (so 0 becomes 1, 9 becomes 10, etc)
                    }

                    output.Write(2, 0); // dim_shift = 0
                    output.Write(2, 0); // name_len = 0
                    output.Write(1, 0); // alpha_associated = 0
                }
            }
            else
            {
                output.Write(2, 0b00); // 0 extra channels
            }

            output.Write(1, 0); // not XYB

            if (this.channels > 2)
            {
                output.Write(1, 1); // color_encoding.all_default = 1 (sRGB)
            }
            else
            {
                output.Write(1, 0); // color_encoding.all_default = 0
                output.Write(1, 0); // color_encoding.want_icc = 0
                output.Write(2, 0b01); // Grayscale
                output.Write(2, 0b01); // D65
                output.Write(1, 0); // No gamma transfer function
                output.Write(2, 0b10); // transfer function: 2 + u(4)
                output.Write(4, 11); // transfer function (specifies sRGB)
                output.Write(2, 1); // relative rendering intent
            }

            output.Write(2, 0b00); // No extensions
            output.Write(1, 1); // all_default transform data
            output.ZeroPadToByte(); // No ICC and no preview. Frame should start at byte boundary.
        }

        // Handcrafted frame header
        output.Write(1, 0); // all_default = 0 (non-default values)
        output.Write(2, 0b00); // regular frame
        output.Write(1, 1); // modular
        output.Write(2, 0b00); // default flags
        output.Write(1, 0); // not Y'Cb'Cr
        output.Write(2, 0b00); // no upsampling

        if (haveAlpha)
        {
            output.Write(2, 0b00); // no alpha upsampling
        }

        output.Write(2, 0b01); // default group size
        output.Write(2, 0b00); // exactly one pass
        output.Write(1, 0); // no custom size or origin
        output.Write(2, 0b00); // Replace blending mode

        if (haveAlpha)
        {
            output.Write(2, 0b00); // Replace blending mode for alpha channel
        }

        output.Write(2, 0b00); // a frame has no name
        output.Write(1, 0); // loop filter is not all_default
        output.Write(1, 0); // no Gaborish transform
        output.Write(2, 0b00); // 0 EPF filters
        output.Write(2, 0b00); // no LF extensions
        output.Write(2, 0b00); // no FH extensions

        output.Write(1, 0); // no TOC permutation
        output.ZeroPadToByte(); // TOC is byte aligned

        Span<int> groupSizes = CollectionsMarshal.AsSpan(this.groupSizes);

        for (int i = 0; i < groupSizes.Length; i++)
        {
            int groupSize = groupSizes[i];

            int bucket = TocBucket(groupSize);
            output.Write(2, (ulong)bucket);
            output.Write(TocBits[bucket] - 2, (ulong)(groupSize - GroupSizeOffset[bucket]));
        }

        output.ZeroPadToByte(); // Groups are byte-aligned

        // Sizes are coded using a special variable-length
        // kind of coding. This method does that here.
        //
        // It has a prefix of 2 bits, followed by the suffix of N
        // bits which depend on the prefix:
        //
        //    prefix 0b00: 9 consecutive bits
        //    prefix 0b01: 13 consecutive bits
        //    prefix 0b10: 18 consecutive bits
        //    prefix 0b11: 30 consecutive bits
        void WriteSize(int size)
        {
            ulong sizeMinus1 = (ulong)size - 1uL;

            if (sizeMinus1 < (1 << 9))
            {
                output.Write(2, 0b00); // 9 bits
                output.Write(9, sizeMinus1);
            }
            else if (sizeMinus1 < (1 << 13))
            {
                output.Write(2, 0b01); // 13 bits
                output.Write(13, sizeMinus1);
            }
            else if (sizeMinus1 < (1 << 18))
            {
                output.Write(2, 0b10); // 18 bits
                output.Write(18, sizeMinus1);
            }
            else
            {
                output.Write(2, 0b11); // 30 bits
                output.Write(30, sizeMinus1);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComputeDcGlobalPadding(Span<int> groupSizes, int acGroupDataOffset, int minDcGlobalSize, bool containsAlpha, bool isLast)
    {
        // Libjxl reference implements this method like this:
        /*
         size_t ComputeDcGlobalPadding(const std::vector<size_t>& group_sizes,
                              size_t ac_group_data_offset,
                              size_t min_dc_global_size, bool have_alpha,
                              bool is_last) {
              std::vector<size_t> new_group_sizes = group_sizes;
              new_group_sizes[0] = min_dc_global_size;
              size_t toc_size = TOCSize(new_group_sizes);
              size_t actual_offset =
                  FrameHeaderSize(have_alpha, is_last) + toc_size + group_sizes[0];
              return ac_group_data_offset - actual_offset;
         }
         */
        // The reference implementation copies the entire vector so that
        // element 0 can be modified without affecting the original.
        // Since TocSize() does not throw, temporarily modify element 0
        // instead, avoiding the allocation and copy.
        int firstItem = groupSizes[0];
        groupSizes[0] = minDcGlobalSize;
        int tocSize = TocSize(groupSizes);
        int actualOffset = FrameHeaderSize(containsAlpha, isLast) + tocSize + firstItem;
        groupSizes[0] = firstItem;
        return acGroupDataOffset - actualOffset;
    }

    private static int PredictPixels<T>(Span<T> pixels, Span<T> pixelsLeft, Span<T> pixelsTop, Span<T> pixelsTopleft, Span<T> residuals, T negativeOne)
        where T : unmanaged
    {
        Vector<T> px = Vector.Create<T>(pixels);
        Vector<T> left = Vector.Create<T>(pixelsLeft);
        Vector<T> top = Vector.Create<T>(pixelsTop);
        Vector<T> topleft = Vector.Create<T>(pixelsTopleft);

        Vector<T> ac = left - topleft;
        Vector<T> ab = left - top;
        Vector<T> bc = top - topleft;
        Vector<T> grad = ac + top;
        Vector<T> d = ab ^ bc;
        Vector<T> zero = Vector<T>.Zero;
        Vector<T> clamp = Vector.ConditionalSelect(Vector.GreaterThan(zero, d), top, left);
        Vector<T> s = ac ^ bc;
        Vector<T> pred = Vector.ConditionalSelect(Vector.GreaterThan(zero, s), grad, clamp);

        Vector<T> res = px - pred;
        Vector<T> resTimes2 = res + res;
        res = Vector.ConditionalSelect(Vector.GreaterThan(zero, res), Vector.Create(negativeOne) - resTimes2, resTimes2);
        res.CopyTo(residuals);

        return FjxlSimdUtils.CountPrefix(Vector.Equals(res, zero));
    }

    private static void EncodeHybridUint000(uint value, out uint token, out uint nbits, out uint bits)
    {
        if (value == 0)
        {
            token = 0;
            nbits = 0;
            bits = 0;
            return;
        }

        uint n = FloorLog2(value);
        token = n + 1;
        nbits = n;
        bits = value - (1u << (int)n);
    }

    private static void GenericEncodeChunk(ReadOnlySpan<uint> residuals, int n, int skip, FjxlPrefixCode code, ref FjxlBitWriter output)
    {
        for (int ix = skip; ix < n; ix++)
        {
            EncodeHybridUint000(residuals[ix], out uint token, out uint nbits, out uint bits);
            output.Write((int)(code.RawLengths[(int)token] + nbits), code.RawCodes[(int)token] | (bits << code.RawLengths[(int)token]));
        }
    }

    /// <summary>
    /// Pair of two vectors.
    /// </summary>
    /// <typeparam name="T">Type of the vector.</typeparam>
    /// <param name="lo">Low vector</param>
    /// <param name="hi">High vector</param>
    private struct VectorPair<T>(T lo, T hi)
        where T : unmanaged
    {
        public T Low = lo;
        public T High = hi;
    }

    internal static class SimdVector32
    {
        public static Vector<int> ValueToToken(Vector<int> vec) => Vector.Create(32) - SimdUtils.Lzcnt(vec);

        public static Vector<int> SaturateSubtract(Vector<int> a, Vector<int> b) => Vector.Max(a, b) - b;

        public static Vector<int> Pow2(Vector<int> x) => SimdUtils.Pow2(x);
    }
}

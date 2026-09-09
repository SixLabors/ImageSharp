// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

/// <summary>
/// Extreme performance JPEG XL encoder which provides minimal lossless compression.
/// It also uses minimal dependencies.
/// </summary>
internal sealed class JxlFastLosslessEncoder
{
    /// <summary>
    /// Specifies maximum number of bytes a frame header may use.
    /// </summary>
    private const int MaxFrameHeaderSize = 5;

    private const int NumRawSymbols = 19;

    private const int NumLz77 = 33;

    /// <summary>
    /// Cache/dictionary size for LZ77
    /// </summary>
    private const int Lz77CacheSize = 32;

    private const int Lz77Offset = 224;

    private const int Lz77MinLength = 7;

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
    private InlineArray4<PrefixCode> hcode;

    private readonly List<short> lookup = [];

    /// <summary>
    /// Bit writer to write the JPEG XL headers.
    /// </summary>
    private readonly BitWriter header;

    /// <summary>
    /// Bit writers for writing JPEG XL groups.
    /// </summary>
    private readonly List<InlineArray4<BitWriter>> groupData = [];

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
    private static ReadOnlySpan<byte> MinimumRawLength => [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// Gets maximum raw lengths for prefix coding.
    /// </summary>
    private static ReadOnlySpan<byte> MaximumRawLength => [7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 10];

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
    private static uint FloorLog2(uint v) => v == 0 ? 0 : 31u - (uint)BitOperations.LeadingZeroCount(v);

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

    private static long GetSectionSize(InlineArray4<BitWriter> groupData)
    {
        long size = 0;

        for (int j = 0; j < 4; j++)
        {
            BitWriter writer = groupData[j];

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

        Span<InlineArray4<BitWriter>> groups = CollectionsMarshal.AsSpan(this.groupData);

        for (int i = 0; i < groups.Length; i++)
        {
            InlineArray4<BitWriter> section = groups[i];

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
        BitWriter output = this.header;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeHybridUintLz77(int value, out int token, out int nBits, out int bits)
    {
        unchecked
        {
            int n = (int)FloorLog2((uint)value);

            if (value < 16)
            {
                token = value;
                nBits = 0;
                bits = 0;
            }
            else
            {
                token = 16 + n - 4;
                nBits = n;
                bits = value - (1 << n);
            }
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

    /// <summary>
    /// The prefix code is used for encoding LZ77-compressed coefficients.
    /// </summary>
    private sealed class PrefixCode
    {
#pragma warning disable SA1401 // Fields should be private

        /// <summary>
        /// Maximum number of raw symbols for prefix coding.
        /// </summary>
        private const int MaxNumSymbols = NumRawSymbols + 1 < NumLz77 ? NumLz77 : NumRawSymbols + 1;

        /// <summary>
        /// Gets or sets the Huffman raw bit lengths.
        /// </summary>
        public InlineArray19<byte> RawLengths;

        /// <summary>
        /// Gets or sets the Huffman raw code values.
        /// </summary>
        public InlineArray19<byte> RawCodes;

        /// <summary>
        /// Gets or sets the Huffman LZ77 bit lengths.
        /// </summary>
        public InlineArray33<byte> Lz77Lengths;

        /// <summary>
        /// Gets or sets the Huffman LZ77 code values.
        /// </summary>
        public InlineArray33<ushort> Lz77Codes;

        /// <summary>
        /// Gets or sets the Huffman LZ77 cache code values.
        /// </summary>
        public InlineArray32<ulong> Lz77CacheBits;

        /// <summary>
        /// Gets or sets the Huffman LZ77 cache bit lengths.
        /// </summary>
        public InlineArray32<byte> Lz77CacheLengths;

        public PrefixCode(Span<ulong> rawCounts, Span<ulong> lz77Counts)
        {
            Span<ulong> level1Counts = stackalloc ulong[NumRawSymbols + 1];
            rawCounts[..NumRawSymbols].CopyTo(level1Counts);

            this.RawCount = NumRawSymbols;

            while (this.RawCount > 0 && level1Counts[this.RawCount - 1] == 0)
            {
                this.RawCount--;
            }

            level1Counts[this.RawCount] = 0;

            for (int i = 0; i < NumLz77; i++)
            {
                level1Counts[this.RawCount] += lz77Counts[i];
            }

            Span<byte> level1Lengths = stackalloc byte[NumRawSymbols + 1];
            level1Lengths.Clear();

            ComputeCodeLengths(level1Counts, this.RawCount + 1, MinimumRawLength, MaximumRawLength, level1Lengths);

            Span<byte> level2Lengths = stackalloc byte[NumLz77];
            Span<byte> minLengths = stackalloc byte[NumLz77];

            level2Lengths.Clear();
            minLengths.Clear();

            int l = 15 - level1Lengths[this.RawCount];
            Span<byte> maxLengths = stackalloc byte[NumLz77];
            maxLengths.Fill((byte)l);

            int numLz77 = NumLz77;
            while (numLz77 > 0 && lz77Counts[numLz77 - 1] == 0)
            {
                numLz77--;
            }

            ComputeCodeLengths(lz77Counts, numLz77, minLengths, maxLengths, level2Lengths);

            level1Lengths[..this.RawCount].CopyTo(this.RawLengths);

            for (int i = 0; i < numLz77; i++)
            {
                this.Lz77Lengths[i] = (byte)(level2Lengths[i] != 0 ? level1Lengths[this.RawCount] + level2Lengths[i] : 0);
            }

            ComputeCanonicalCode(this.RawLengths, this.RawCodes, this.Lz77Lengths, this.Lz77Codes);

            // Prepare the LZ77 cache
            for (int count = 0; count < Lz77CacheSize; count++)
            {
                EncodeHybridUintLz77(count, out int token, out int nbits, out int bits);
                this.Lz77CacheLengths[count] = (byte)(this.Lz77Lengths[token] + nbits + this.RawLengths[0]);
                this.Lz77CacheBits[count] =
                    (ulong)((((bits << this.Lz77Lengths[token]) | this.Lz77Codes[token]) << this.RawLengths[0]) |
                    this.RawLengths[0]);
            }
        }

        /// <summary>
        /// Gets a lookup used to reverse integers bit-wise.
        /// </summary>
        private static ReadOnlySpan<ushort> ReverseNibbleLookup =>
        [
            0b0000, 0b1000, 0b0100, 0b1100, 0b0010, 0b1010, 0b0110, 0b1110,
            0b0001, 0b1001, 0b0101, 0b1101, 0b0011, 0b1011, 0b0111, 0b1111,
        ];

#pragma warning restore SA1401 // Fields should be private

        /// <summary>
        /// Gets or sets the number of raw codes.
        /// </summary>
        public int RawCount { get; set; }

        /// <summary>
        /// Reverses the integer bit-wise.
        /// </summary>
        /// <param name="nbits">Number of bits for the integer.</param>
        /// <param name="bits">Actual bits to reverse.</param>
        /// <returns>
        /// Input integer but reversed. F.e. 10010 becomes 01001.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort BitReverse(int nbits, ushort bits)
        {
            unchecked
            {
                ushort rev16 = (ushort)((ReverseNibbleLookup[bits & 0xF] << 12) |
                     (ReverseNibbleLookup[(bits >> 4) & 0xF] << 8) |
                     (ReverseNibbleLookup[(bits >> 8) & 0xF] << 4) |
                     ReverseNibbleLookup[bits >> 12]);
                return (ushort)(rev16 >> (16 - nbits));
            }
        }

        private static void ComputeCanonicalCode(Span<byte> firstChunkLengths, Span<byte> firstChunkCodes, Span<byte> secondChunkLengths, Span<ushort> secondChunkCodes)
        {
            const int maxCodeLength = 15;

            Span<byte> codeLengthCounts = stackalloc byte[maxCodeLength + 1];
            codeLengthCounts.Clear();

            for (int i = 0; i < firstChunkCodes.Length; i++)
            {
                codeLengthCounts[firstChunkLengths[i]]++;

                if (firstChunkLengths[i] > 8)
                {
                    throw new InvalidOperationException("First chunk length is too large");
                }

                if (firstChunkLengths[i] <= 0)
                {
                    throw new InvalidOperationException("First chunk length cannot be <= 0");
                }
            }

            for (int i = 0; i < secondChunkCodes.Length; i++)
            {
                codeLengthCounts[secondChunkLengths[i]]++;

                if (secondChunkLengths[i] > maxCodeLength)
                {
                    throw new InvalidOperationException("Second chunk length is too large");
                }
            }

            Span<ushort> nextCode = stackalloc ushort[maxCodeLength + 1];
            nextCode.Clear();

            ushort code = 0;

            for (int i = 1; i < maxCodeLength + 1; i++)
            {
                code = unchecked((ushort)((code + codeLengthCounts[i - 1]) << 1));
                nextCode[i] = code;
            }

            unchecked
            {
                for (int i = 0; i < firstChunkCodes.Length; i++)
                {
                    firstChunkCodes[i] = (byte)BitReverse(firstChunkLengths[i], nextCode[firstChunkLengths[i]]++);
                }

                for (int i = 0; i < secondChunkCodes.Length; i++)
                {
                    secondChunkCodes[i] = (byte)BitReverse(secondChunkLengths[i], nextCode[secondChunkLengths[i]]++);
                }
            }
        }

        private static void ComputeCodeLengthsNonZeroImpl<T>(
            Span<ulong> freqs,
            int n,
            int precision,
            T infty,
            Span<byte> minLimit,
            Span<byte> maxLimit,
            Span<byte> nbits)
            where T : unmanaged, INumber<T>
        {
            DebugGuard.MustBeLessThan(precision, 15, nameof(precision));
            DebugGuard.MustBeLessThanOrEqualTo(n, MaxNumSymbols, nameof(n));

            int scale = 1 << precision;
            int width = scale + 1;

            Span<T> dynp = stackalloc T[width * (n + 1)];
            dynp.Fill(infty);
            dynp[0] = T.Zero;

            for (int sym = 0; sym < n; sym++)
            {
                for (int bits = minLimit[sym]; bits <= maxLimit[sym]; bits++)
                {
                    int offsetDelta = 1 << (precision - bits);
                    T cost = T.CreateChecked(freqs[sym]) * T.CreateChecked(bits);

                    for (int off = 0; off + offsetDelta <= scale; off++)
                    {
                        int current = (sym * width) + off;
                        int next = ((sym + 1) * width) + off + offsetDelta;

                        dynp[next] = T.Min(dynp[current] + cost, dynp[next]);
                    }
                }
            }

            int offFinal = scale;

            for (int sym = n - 1; sym >= 0; sym--)
            {
                if (offFinal <= 0)
                {
                    throw new InvalidOperationException("Offset should be greater than zero");
                }

                for (int bits = minLimit[sym]; bits <= maxLimit[sym]; bits++)
                {
                    int offsetDelta = 1 << (precision - bits);

                    if (offsetDelta <= offFinal)
                    {
                        int current = (sym * width) + offFinal;
                        int previous = (sym * width) + offFinal - offsetDelta;

                        T cost = T.CreateChecked(freqs[sym]) * T.CreateChecked(bits);

                        if (dynp[current] == dynp[previous] + cost)
                        {
                            offFinal -= offsetDelta;
                            nbits[sym] = (byte)bits;
                            break;
                        }
                    }
                }
            }
        }

        private static void ComputeCodeLengthsNonZero(Span<ulong> freqs, int n, Span<byte> minLimit, Span<byte> maxLimit, Span<byte> nbits)
        {
            int precision = 0;
            int shortestLength = 255;
            ulong frequencySum = 0;

            for (int i = 0; i < n; i++)
            {
                frequencySum += freqs[i];

                if (minLimit[i] < 1)
                {
                    minLimit[i] = 1;
                }

                precision = Math.Max(maxLimit[i], precision);
                shortestLength = Math.Min(minLimit[i], shortestLength);
            }

            precision -= shortestLength - 1;
            ulong infinity = frequencySum * (ulong)precision;

            if (infinity < uint.MaxValue / 2)
            {
                ComputeCodeLengthsNonZeroImpl(freqs, n, precision, (uint)infinity, minLimit, maxLimit, nbits);
            }
            else
            {
                ComputeCodeLengthsNonZeroImpl(freqs, n, precision, infinity, minLimit, maxLimit, nbits);
            }
        }

        private static void ComputeCodeLengths(Span<ulong> freqs, int n, ReadOnlySpan<byte> minLimitIn, ReadOnlySpan<byte> maxLimitIn, Span<byte> nbits)
        {
            DebugGuard.MustBeLessThanOrEqualTo(n, MaxNumSymbols, nameof(n));

            Span<ulong> compactFreqs = stackalloc ulong[MaxNumSymbols];
            Span<byte> minLimit = stackalloc byte[MaxNumSymbols];
            Span<byte> maxLimit = stackalloc byte[MaxNumSymbols];

            int ni = 0;
            for (int i = 0; i < n; i++)
            {
                if (freqs[i] != 0)
                {
                    compactFreqs[ni] = freqs[i];
                    minLimit[ni] = minLimitIn[i];
                    maxLimit[ni] = maxLimitIn[i];
                    ni++;
                }
            }

            compactFreqs[ni..].Clear();
            minLimit[ni..].Clear();
            maxLimit[ni..].Clear();

            Span<byte> numBits = stackalloc byte[MaxNumSymbols];
            numBits.Clear();

            ComputeCodeLengthsNonZero(compactFreqs, ni, minLimit, maxLimit, numBits);

            ni = 0;

            for (int i = 0; i < n; i++)
            {
                nbits[i] = 0;
                if (freqs[i] != 0)
                {
                    nbits[i] = numBits[ni++];
                }
            }
        }

        /// <summary>
        /// Writes this LZ77 prefix code into the bit-stream.
        /// </summary>
        /// <param name="writer">The bit-stream to write the prefix code into.</param>
        public void Write(BitWriter writer)
        {
            Span<ulong> codeLengthCounts = stackalloc ulong[32].Slice(0, 18);
            codeLengthCounts.Clear();
            codeLengthCounts[17] = 3 + (2 * (NumLz77 - 1));

            for (int i = 0; i < 19; i++)
            {
                byte rawLength = this.RawLengths[i];

                codeLengthCounts[rawLength]++;
            }

            for (int i = 0; i < 33; i++)
            {
                byte lz77Length = this.Lz77Lengths[i];

                codeLengthCounts[lz77Length]++;
            }

            // Lengths for representing the code length
            Span<byte> codeLengthLengths = stackalloc byte[32].Slice(0, 18);
            Span<byte> codeLengthLengthsMinimum = stackalloc byte[32].Slice(0, 18);
            Span<byte> codeLengthLengthsMaximum = stackalloc byte[32].Slice(0, 18);

            codeLengthLengths.Clear();
            codeLengthLengthsMinimum.Clear();
            codeLengthLengthsMaximum.Fill(5);

            ComputeCodeLengths(codeLengthCounts, 18, codeLengthLengthsMinimum, codeLengthLengthsMaximum, codeLengthLengths);

            writer.Write(2, 0b00); // HSKIP = 0 (Don't skip code lengths)

            // As per Brotli RFC
            Span<byte> codeLengthOrder = [1, 2, 3, 4,  0,  5,  17, 6,  16,
                                          7, 8, 9, 10, 11, 12, 13, 14, 15];

            // Lengths & codes for representing lengths of code lengths
            Span<byte> codeLengthLengthLengths = [2, 4, 3, 2, 2, 4];
            Span<byte> codeLengthLengthCodes = [0, 7, 3, 2, 1, 15];

            // Maximum number of code lengths
            int numCodeLengths = 18;
            while (codeLengthLengths[codeLengthOrder[numCodeLengths - 1]] == 0)
            {
                numCodeLengths--;
            }

            // Max bits written in this loop: 18 * 4 = 72
            for (int i = 0; i < numCodeLengths; i++)
            {
                int symbol = codeLengthLengths[codeLengthOrder[i]];
                writer.Write(codeLengthLengthLengths[symbol], codeLengthLengthCodes[symbol]);
            }

            Span<ushort> codeLengthBits = stackalloc ushort[32].Slice(0, 18);
            codeLengthBits.Clear();
            ComputeCanonicalCode([], [], codeLengthLengths, codeLengthBits);

            for (int i = 0; i < 19; i++)
            {
                byte rawLength = this.RawLengths[i];

                writer.Write(codeLengthLengths[rawLength], codeLengthBits[rawLength]);
            }

            int numLz77 = NumLz77;
            while (this.Lz77Lengths[numLz77 - 1] == 0)
            {
                numLz77--;
            }

            // Max bits in this block: 24
            writer.Write(codeLengthLengths[17], codeLengthBits[17]);
            writer.Write(3, 0b010); // 5
            writer.Write(codeLengthLengths[17], codeLengthBits[17]);
            writer.Write(3, 0b000); // (5 - 2) * 8 + 3 = 27
            writer.Write(codeLengthLengths[17], codeLengthBits[17]);
            writer.Write(3, 0b010); // (27 - 2) * 8 + 5 = 205

            // Encode LZ77 symbols with values 224 + i.
            // Max. bits in this loop: 33 * 5 = 165
            for (int i = 0; i < numLz77; i++)
            {
                writer.Write(codeLengthLengths[this.Lz77Lengths[i]], codeLengthBits[this.Lz77Lengths[i]]);
            }
        }
    }

    /// <summary>
    /// Simple MSB-first bit-stream writer implementation built on top
    /// of a stream.
    /// </summary>
    /// <param name="stream">Output bytes are written here.</param>
    private sealed class BitWriter(Stream stream) : IDisposable
    {
        /// <summary>
        /// Temporary cache used to store pending written bits
        /// before they're written to the output stream.
        /// </summary>
        private ulong buffer;

        /// <summary>
        /// Gets the total number of bytes written to the output buffer so far.
        /// </summary>
        public long BytesWritten { get; private set; }

        /// <summary>
        /// Gets the number of bits actively in the bit cache.
        /// This is used to track how many bits were written into
        /// the cache prior to sending the cache to the stream.
        /// </summary>
        public int BitsInBuffer { get; private set; }

        /// <summary>
        /// Writes the specified bits in the Most Significant Byte (MSB)
        /// order.
        /// </summary>
        /// <param name="count">Represents the number of bits to write to the bit-stream.</param>
        /// <param name="bits">Represents the value to write to the bit-stream.</param>
        public void Write(int count, ulong bits)
        {
            DebugGuard.MustBeBetweenOrEqualTo(count, 0, 56, nameof(count));

            if (count < 64)
            {
                bits &= (1UL << count) - 1;
            }

            this.buffer |= bits << this.BitsInBuffer;
            this.BitsInBuffer += count;

            this.FlushBytes();
        }

        /// <summary>
        /// Internal method used to flush bytes from the cache
        /// (<see cref="buffer"/>) into the output stream.
        /// </summary>
        private void FlushBytes()
        {
            int bytes = this.BitsInBuffer / 8;

            for (int i = 0; i < bytes; i++)
            {
                stream.WriteByte((byte)this.buffer);
                this.BytesWritten++;
                this.buffer >>= 8;
            }

            this.BitsInBuffer -= bytes * 8;
        }

        /// <summary>
        /// Used by the dispose method to flush the remaining bits
        /// that are not byte-aligned. F.e. if we dispose this reader
        /// and we have 5 bits left, those final 5 bits are set to all 0
        /// and the byte is written to the stream.
        /// </summary>
        public void ZeroPadToByte()
        {
            if (this.BitsInBuffer != 0)
            {
                this.Write(8 - this.BitsInBuffer, 0);
            }
        }

        /// <summary>
        /// Flushes out the final bytes.
        /// </summary>
        public void Dispose() => this.ZeroPadToByte();
    }
}

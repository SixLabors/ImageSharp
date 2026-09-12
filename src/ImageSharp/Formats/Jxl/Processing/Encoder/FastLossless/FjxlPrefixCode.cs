// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless;

/// <summary>
/// The prefix code is used for encoding LZ77-compressed coefficients.
/// </summary>
internal sealed class FjxlPrefixCode
{
#pragma warning disable SA1401 // Fields should be private

    /// <summary>
    /// Maximum number of raw symbols for prefix coding.
    /// </summary>
    private const int MaxNumSymbols = JxlFastLosslessEncoder.NumRawSymbols + 1 < JxlFastLosslessEncoder.NumLz77 ? JxlFastLosslessEncoder.NumLz77 : JxlFastLosslessEncoder.NumRawSymbols + 1;

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

    public FjxlPrefixCode(Span<ulong> rawCounts, Span<ulong> lz77Counts)
    {
        Span<ulong> level1Counts = stackalloc ulong[JxlFastLosslessEncoder.NumRawSymbols + 1];
        rawCounts[..JxlFastLosslessEncoder.NumRawSymbols].CopyTo(level1Counts);

        this.RawCount = JxlFastLosslessEncoder.NumRawSymbols;

        while (this.RawCount > 0 && level1Counts[this.RawCount - 1] == 0)
        {
            this.RawCount--;
        }

        level1Counts[this.RawCount] = 0;

        for (int i = 0; i < JxlFastLosslessEncoder.NumLz77; i++)
        {
            level1Counts[this.RawCount] += lz77Counts[i];
        }

        Span<byte> level1Lengths = stackalloc byte[JxlFastLosslessEncoder.NumRawSymbols + 1];
        level1Lengths.Clear();

        ComputeCodeLengths(level1Counts, this.RawCount + 1, JxlFastLosslessEncoder.MinimumRawLength, JxlFastLosslessEncoder.MaximumRawLength, level1Lengths);

        Span<byte> level2Lengths = stackalloc byte[JxlFastLosslessEncoder.NumLz77];
        Span<byte> minLengths = stackalloc byte[JxlFastLosslessEncoder.NumLz77];

        level2Lengths.Clear();
        minLengths.Clear();

        int l = 15 - level1Lengths[this.RawCount];
        Span<byte> maxLengths = stackalloc byte[JxlFastLosslessEncoder.NumLz77];
        maxLengths.Fill((byte)l);

        int numLz77 = JxlFastLosslessEncoder.NumLz77;
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
        for (int count = 0; count < JxlFastLosslessEncoder.Lz77CacheSize; count++)
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
    public void Write(FjxlBitWriter writer)
    {
        Span<ulong> codeLengthCounts = stackalloc ulong[32].Slice(0, 18);
        codeLengthCounts.Clear();
        codeLengthCounts[17] = 3 + (2 * (JxlFastLosslessEncoder.NumLz77 - 1));

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

        int numLz77 = JxlFastLosslessEncoder.NumLz77;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeHybridUintLz77(int value, out int token, out int nBits, out int bits)
    {
        unchecked
        {
            int n = (int)JxlFastLosslessEncoder.FloorLog2((uint)value);

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
}

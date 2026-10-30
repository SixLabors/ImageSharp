// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;

/// <summary>
/// Reads JPEG XL ANS distributions and builds compact alias buckets.
/// </summary>
internal static class JxlAnsDecoder
{
    /// <summary>
    /// The base-two logarithm of the normalized distribution total.
    /// </summary>
    public const int ProbabilityBits = 12;

    /// <summary>
    /// The normalized distribution total.
    /// </summary>
    public const int ProbabilityTotal = 1 << ProbabilityBits;

    /// <summary>
    /// Reads one distribution and builds its alias table without allocating a 4096-entry symbol table.
    /// </summary>
    /// <param name="reader">The bounded codestream reader.</param>
    /// <param name="buckets">The caller-owned table, with 32, 64, 128 or 256 entries.</param>
    public static void ReadDistribution(ref JxlBitReader reader, Span<Bucket> buckets)
    {
        Span<int> frequencies = stackalloc int[256];
        frequencies = frequencies[..buckets.Length];
        frequencies.Clear();

        // The first two flags select a one/two-symbol distribution, a flat alphabet, or the general form.
        // Every form produces exactly 4096 units of probability before the shared alias construction below.
        if (reader.ReadBoolean())
        {
            bool twoSymbols = reader.ReadBoolean();
            int first = ReadVarUInt8(ref reader);
            int second = twoSymbols ? ReadVarUInt8(ref reader) : first;
            if (first >= buckets.Length || second >= buckets.Length || (twoSymbols && first == second))
            {
                throw new InvalidImageContentException("The JPEG XL ANS distribution contains an invalid symbol.");
            }

            int frequency = twoSymbols ? (int)reader.ReadBits(ProbabilityBits) : ProbabilityTotal;
            frequencies[second] = ProbabilityTotal - frequency;
            frequencies[first] = frequency;
        }
        else if (reader.ReadBoolean())
        {
            int alphabetSize = ReadVarUInt8(ref reader) + 1;
            if (alphabetSize > frequencies.Length)
            {
                throw new InvalidImageContentException("The JPEG XL ANS alphabet exceeds its table.");
            }

            int quotient = ProbabilityTotal / alphabetSize;
            int remainder = ProbabilityTotal % alphabetSize;

            // Division leaves a remainder when the alphabet size does not divide 4096. The first symbols
            // receive one extra unit, preserving the exact normalized total in the format's specified order.
            frequencies[..remainder].Fill(quotient + 1);
            frequencies[remainder..alphabetSize].Fill(quotient);
        }
        else
        {
            ReadComplexDistribution(ref reader, frequencies);
        }

        int bucketSize = ProbabilityTotal / buckets.Length;
        int singleSymbol = frequencies.IndexOf(ProbabilityTotal);
        if (singleSymbol >= 0)
        {
            // All states select the same symbol. Preserve the state's low-bit position through AliasOffset
            // so the ANS update still uses the full probability interval, without a special symbol-reading branch.
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new Bucket
                {
                    AliasSymbol = (byte)singleSymbol,
                    Frequency = (ushort)frequencies[i],
                    AliasOffset = (ushort)(bucketSize * i),
                    AliasFrequencyXor = (ushort)(frequencies[i] ^ ProbabilityTotal)
                };
            }

            return;
        }

        Span<int> cutoff = stackalloc int[256];
        Span<int> alias = stackalloc int[256];
        Span<int> offset = stackalloc int[256];
        Span<int> underfull = stackalloc int[256];
        Span<int> overfull = stackalloc int[256];
        int underCount = 0;
        int overCount = 0;

        // A bucket initially owns its symbol's frequency. Keep only underfull and overfull bucket indices
        // on bounded stack workspaces; no table scratch survives distribution construction.
        for (int i = 0; i < buckets.Length; i++)
        {
            cutoff[i] = frequencies[i];
            alias[i] = i;
            offset[i] = 0;
            if (frequencies[i] < bucketSize)
            {
                underfull[underCount++] = i;
            }
            else if (frequencies[i] > bucketSize)
            {
                overfull[overCount++] = i;
            }
        }

        // Transfer probability mass from the last overfull bucket to the last underfull bucket.
        // The stack order is part of the alias layout: it must match the encoder's symbol mapping.
        while (underCount != 0 && overCount != 0)
        {
            int over = overfull[--overCount];
            int under = underfull[--underCount];
            cutoff[over] -= bucketSize - cutoff[under];
            alias[under] = over;
            offset[under] = cutoff[over];
            if (cutoff[over] < bucketSize)
            {
                underfull[underCount++] = over;
            }
            else if (cutoff[over] > bucketSize)
            {
                overfull[overCount++] = over;
            }
        }

        // Encode each split once. XOR stores the alternate frequency relative to the current frequency;
        // the offset maps an aliased position into that symbol's cumulative probability interval.
        for (int i = 0; i < buckets.Length; i++)
        {
            // A full bucket maps every position to itself. Encoding it as an unconditional alias
            // avoids a separate full-bucket branch when decoding each symbol.
            bool full = cutoff[i] == bucketSize;
            buckets[i] = new Bucket
            {
                Frequency = (ushort)frequencies[i],
                AliasSymbol = (byte)(full ? i : alias[i]),
                Cutoff = (byte)(full ? 0 : cutoff[i]),
                AliasOffset = (ushort)(full ? 0 : offset[i] - cutoff[i]),
                AliasFrequencyXor = (ushort)(full ? 0 : frequencies[i] ^ frequencies[alias[i]])
            };
        }
    }

    /// <summary>
    /// Reads the zero-or-exponent encoding used for eight-bit ANS alphabet values.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    /// <returns>The value from zero to 255.</returns>
    public static int ReadVarUInt8(ref JxlBitReader reader)
    {
        if (!reader.ReadBoolean())
        {
            return 0;
        }

        int bits = (int)reader.ReadBits(3);
        return (1 << bits) + (int)reader.ReadBits(bits);
    }

    /// <summary>
    /// Reads logarithmic frequencies, repeated frequencies and the omitted remainder frequency.
    /// </summary>
    /// <param name="reader">The bounded codestream reader.</param>
    /// <param name="frequencies">The zero-initialized distribution storage.</param>
    private static void ReadComplexDistribution(ref JxlBitReader reader, Span<int> frequencies)
    {
        int bits = 0;
        while (bits < 3 && reader.ReadBoolean())
        {
            bits++;
        }

        int shift = (int)reader.ReadBits(bits) + (1 << bits) - 1;
        int alphabetSize = ReadVarUInt8(ref reader) + 3;
        if (shift > 13 || alphabetSize > frequencies.Length)
        {
            throw new InvalidImageContentException("The JPEG XL ANS distribution parameters are invalid.");
        }

        Span<int> codes = stackalloc int[256];
        Span<int> repeats = stackalloc int[256];
        codes.Clear();
        repeats.Clear();
        int omitted = -1;

        // Zero denotes an absent frequency, so it cannot identify the omitted positive frequency.
        // libjxl rejects a complex distribution containing only zero codes and repeat markers.
        int maximumCode = 0;
        for (int i = 0; i < alphabetSize; i++)
        {
            int code = ReadFrequencyCode(ref reader);
            codes[i] = code;
            if (code == 13)
            {
                int count = ReadVarUInt8(ref reader) + 4;
                if (count > alphabetSize - i)
                {
                    throw new InvalidImageContentException("The JPEG XL ANS frequency run exceeds its alphabet.");
                }

                repeats[i] = count;
                i += count - 1;
            }
            else if (code > maximumCode)
            {
                maximumCode = code;
                omitted = i;
            }
        }

        if (omitted < 0 || (omitted + 1 < alphabetSize && codes[omitted + 1] == 13))
        {
            throw new InvalidImageContentException("The JPEG XL ANS omitted frequency is invalid.");
        }

        int total = 0;
        int previous = 0;

        // One frequency is omitted from the stream. Decode all other positive frequencies and repeated
        // runs, then recover that omitted value from the normalized total without rounding a probability.
        for (int i = 0; i < alphabetSize; i++)
        {
            if (repeats[i] != 0)
            {
                int count = repeats[i];
                frequencies.Slice(i, count).Fill(previous);
                total += previous * count;
                i += count - 1;
            }
            else if (i == omitted)
            {
                previous = 0;
            }
            else
            {
                int code = codes[i];
                int frequency = code;
                if (code > 1)
                {
                    // The code supplies the magnitude; shift controls how many lower frequency bits follow.
                    // Precision is bounded by that magnitude, so every frequency remains a positive integer.
                    int exponent = code - 1;
                    int precision = Math.Clamp(shift - ((ProbabilityBits - exponent) >> 1), 0, exponent);
                    frequency = (1 << exponent) + ((int)reader.ReadBits(precision) << (exponent - precision));
                }

                frequencies[i] = previous = frequency;
                total += frequency;
            }
        }

        if (total >= ProbabilityTotal)
        {
            throw new InvalidImageContentException("The JPEG XL ANS frequencies exceed their normalized total.");
        }

        frequencies[omitted] = ProbabilityTotal - total;
    }

    /// <summary>
    /// Reads the fixed prefix code used for logarithmic ANS frequencies.
    /// </summary>
    /// <param name="reader">The codestream bit reader.</param>
    /// <returns>The frequency code, including the repeat marker.</returns>
    private static int ReadFrequencyCode(ref JxlBitReader reader)
    {
        // Each entry packs its consumed bit count above its decoded symbol. The table is fixed by the format.
        ReadOnlySpan<ushort> table =
        [
            778, 1804, 775, 1027, 774, 776, 777, 1029, 778, 1028, 775, 1025, 774, 776, 777, 1026,
            778, 1280, 775, 1027, 774, 776, 777, 1029, 778, 1028, 775, 1025, 774, 776, 777, 1026,
            778, 1547, 775, 1027, 774, 776, 777, 1029, 778, 1028, 775, 1025, 774, 776, 777, 1026,
            778, 1280, 775, 1027, 774, 776, 777, 1029, 778, 1028, 775, 1025, 774, 776, 777, 1026,
            778, 1805, 775, 1027, 774, 776, 777, 1029, 778, 1028, 775, 1025, 774, 776, 777, 1026,
            778, 1280, 775, 1027, 774, 776, 777, 1029, 778, 1028, 775, 1025, 774, 776, 777, 1026,
            778, 1547, 775, 1027, 774, 776, 777, 1029, 778, 1028, 775, 1025, 774, 776, 777, 1026,
            778, 1280, 775, 1027, 774, 776, 777, 1029, 778, 1028, 775, 1025, 774, 776, 777, 1026
        ];

        ushort entry = table[(int)reader.PeekBits(7)];
        reader.ConsumeBits(entry >> 8);
        return entry & 255;
    }

    /// <summary>
    /// Stores one eight-byte alias bucket. A table needs at most 256 buckets per distribution.
    /// </summary>
    public struct Bucket
    {
        /// <summary>
        /// The alternate symbol used at and above the cutoff.
        /// </summary>
        public byte AliasSymbol;

        /// <summary>
        /// The first position mapped to the alternate symbol.
        /// </summary>
        public byte Cutoff;

        /// <summary>
        /// The current symbol's normalized frequency.
        /// </summary>
        public ushort Frequency;

        /// <summary>
        /// The alternate symbol's cumulative position adjustment.
        /// </summary>
        public ushort AliasOffset;

        /// <summary>
        /// The XOR of the current and alternate frequencies.
        /// </summary>
        public ushort AliasFrequencyXor;
    }
}

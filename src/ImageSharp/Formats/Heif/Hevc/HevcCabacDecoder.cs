// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Decodes context-adaptive and bypass-coded binary values from one bounded HEVC entropy substream.
/// </summary>
internal ref struct HevcCabacDecoder
{
    /// <summary>
    /// The least-probable-symbol subrange for each probability state and current range class.
    /// </summary>
    private static readonly byte[] LeastProbableSymbolRanges =
    [
        128, 176, 208, 240, 128, 167, 197, 227, 128, 158, 187, 216, 123, 150, 178, 205,
        116, 142, 169, 195, 111, 135, 160, 185, 105, 128, 152, 175, 100, 122, 144, 166,
        95, 116, 137, 158, 90, 110, 130, 150, 85, 104, 123, 142, 81, 99, 117, 135,
        77, 94, 111, 128, 73, 89, 105, 122, 69, 85, 100, 116, 66, 80, 95, 110,
        62, 76, 90, 104, 59, 72, 86, 99, 56, 69, 81, 94, 53, 65, 77, 89,
        51, 62, 73, 85, 48, 59, 69, 80, 46, 56, 66, 76, 43, 53, 63, 72,
        41, 50, 59, 69, 39, 48, 56, 65, 37, 45, 54, 62, 35, 43, 51, 59,
        33, 41, 48, 56, 32, 39, 46, 53, 30, 37, 43, 50, 29, 35, 41, 48,
        27, 33, 39, 45, 26, 31, 37, 43, 24, 30, 35, 41, 23, 28, 33, 39,
        22, 27, 32, 37, 21, 26, 30, 35, 20, 24, 29, 33, 19, 23, 27, 31,
        18, 22, 26, 30, 17, 21, 25, 28, 16, 20, 23, 27, 15, 19, 22, 25,
        14, 18, 21, 24, 14, 17, 20, 23, 13, 16, 19, 22, 12, 15, 18, 21,
        12, 14, 17, 20, 11, 14, 16, 19, 11, 13, 15, 18, 10, 12, 15, 17,
        10, 12, 14, 16, 9, 11, 13, 15, 9, 11, 12, 14, 8, 10, 12, 14,
        8, 9, 11, 13, 7, 9, 11, 12, 7, 9, 10, 12, 7, 8, 10, 11,
        6, 8, 9, 11, 6, 7, 9, 10, 6, 7, 8, 9, 2, 2, 2, 2
    ];

    /// <summary>
    /// The normalization shift for each quantized least-probable-symbol range.
    /// </summary>
    private static readonly byte[] LeastProbableSymbolNormalizationShifts =
    [
        6, 5, 4, 4,
        3, 3, 3, 3,
        2, 2, 2, 2,
        2, 2, 2, 2,
        1, 1, 1, 1,
        1, 1, 1, 1,
        1, 1, 1, 1,
        1, 1, 1, 1
    ];

    /// <summary>
    /// The complete bounded entropy-substream bytes.
    /// </summary>
    private readonly ReadOnlySpan<byte> data;

    /// <summary>
    /// The zero-based offset of the next byte that can refill the arithmetic value register.
    /// </summary>
    private int byteOffset;

    /// <summary>
    /// The current arithmetic interval width.
    /// </summary>
    private uint range;

    /// <summary>
    /// The current arithmetic code value, scaled by seven fractional bits.
    /// </summary>
    private uint value;

    /// <summary>
    /// The number of normalization shifts remaining before the value register requires another byte.
    /// </summary>
    private int bitsNeeded;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCabacDecoder"/> struct.
    /// </summary>
    /// <param name="data">The bytes of one independently bounded HEVC entropy substream.</param>
    /// <exception cref="InvalidImageContentException">The entropy substream is shorter than its initial value register.</exception>
    public HevcCabacDecoder(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
        {
            throw new InvalidImageContentException("The HEVC CABAC substream is truncated.");
        }

        this.data = data;
        this.byteOffset = 2;
        this.range = 510;
        this.value = ((uint)data[0] << 8) | data[1];
        this.bitsNeeded = -8;
    }

    /// <summary>
    /// Gets the number of whole entropy-substream bytes loaded into the arithmetic decoder.
    /// </summary>
    public readonly int BytesConsumed => this.byteOffset;

    /// <summary>
    /// Decodes one context-adaptive binary value and advances its probability state.
    /// </summary>
    /// <param name="context">The adaptive probability context selected for the syntax element.</param>
    /// <returns>The decoded binary value.</returns>
    /// <exception cref="InvalidImageContentException">The entropy substream ends while normalizing the decoded value.</exception>
    public bool ReadDecision(ref HevcCabacContext context)
    {
        int rangeClass = ((int)this.range >> 6) - 4;
        uint leastProbableSymbolRange = LeastProbableSymbolRanges[(context.StateIndex * 4) + rangeClass];
        this.range -= leastProbableSymbolRange;
        uint scaledRange = this.range << 7;

        if (this.value < scaledRange)
        {
            bool symbol = context.MostProbableSymbol;
            context.UpdateMostProbableSymbol();

            if (scaledRange < (256U << 7))
            {
                // Renormalization shifts both registers together so their comparison continues to describe the
                // same arithmetic interval; a byte is loaded only when the buffered fractional bits are exhausted.
                this.range = scaledRange >> 6;
                this.value <<= 1;
                if (++this.bitsNeeded == 0)
                {
                    this.bitsNeeded = -8;
                    this.value += this.ReadByte();
                }
            }

            return symbol;
        }

        bool leastProbableSymbol = !context.MostProbableSymbol;
        int normalizationShift = LeastProbableSymbolNormalizationShifts[(int)(leastProbableSymbolRange >> 3)];
        this.value = (this.value - scaledRange) << normalizationShift;
        this.range = leastProbableSymbolRange << normalizationShift;
        context.UpdateLeastProbableSymbol();
        this.bitsNeeded += normalizationShift;
        if (this.bitsNeeded >= 0)
        {
            this.value += (uint)this.ReadByte() << this.bitsNeeded;
            this.bitsNeeded -= 8;
        }

        return leastProbableSymbol;
    }

    /// <summary>
    /// Decodes one equal-probability binary value without changing an adaptive context.
    /// </summary>
    /// <returns>The decoded binary value.</returns>
    /// <exception cref="InvalidImageContentException">The entropy substream ends while loading the decoded value.</exception>
    public bool ReadBypass()
    {
        if (this.range == 256)
        {
            return this.ReadAlignedBypassBits(1) != 0;
        }

        this.value <<= 1;
        if (++this.bitsNeeded >= 0)
        {
            this.bitsNeeded = -8;
            this.value += this.ReadByte();
        }

        uint scaledRange = this.range << 7;
        if (this.value < scaledRange)
        {
            return false;
        }

        this.value -= scaledRange;
        return true;
    }

    /// <summary>
    /// Decodes a most-significant-bit-first sequence of equal-probability binary values.
    /// </summary>
    /// <param name="bitCount">The number of values to decode.</param>
    /// <returns>The decoded unsigned value.</returns>
    /// <exception cref="InvalidImageContentException">The entropy substream ends while loading the decoded value.</exception>
    public uint ReadBypassBits(int bitCount)
    {
        DebugGuard.MustBeBetweenOrEqualTo(bitCount, 0, 32, nameof(bitCount));
        if (this.range == 256)
        {
            return this.ReadAlignedBypassBits(bitCount);
        }

        uint bins = 0;
        int remaining = bitCount;
        while (remaining > 8)
        {
            this.value = (this.value << 8) + ((uint)this.ReadByte() << (8 + this.bitsNeeded));
            uint scaledRange = this.range << 15;
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                bins <<= 1;
                scaledRange >>= 1;
                if (this.value >= scaledRange)
                {
                    bins++;
                    this.value -= scaledRange;
                }
            }

            remaining -= 8;
        }

        this.bitsNeeded += remaining;
        this.value <<= remaining;
        if (this.bitsNeeded >= 0)
        {
            this.value += (uint)this.ReadByte() << this.bitsNeeded;
            this.bitsNeeded -= 8;
        }

        uint finalScaledRange = this.range << (remaining + 7);
        for (int bitIndex = 0; bitIndex < remaining; bitIndex++)
        {
            bins <<= 1;
            finalScaledRange >>= 1;
            if (this.value >= finalScaledRange)
            {
                bins++;
                this.value -= finalScaledRange;
            }
        }

        return bins;
    }

    /// <summary>
    /// Selects the byte-aligned equal-probability range used by aligned bypass syntax.
    /// </summary>
    public void AlignBypass() => this.range = 256;

    /// <summary>
    /// Decodes the binary value that terminates a coding-tree block or entropy substream.
    /// </summary>
    /// <returns><see langword="true"/> when the current entropy substream terminates; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidImageContentException">The entropy substream ends while normalizing a non-terminating value.</exception>
    public bool ReadTerminate()
    {
        this.range -= 2;
        uint scaledRange = this.range << 7;
        if (this.value >= scaledRange)
        {
            return true;
        }

        if (scaledRange < (256U << 7))
        {
            this.range = scaledRange >> 6;
            this.value <<= 1;
            if (++this.bitsNeeded == 0)
            {
                this.bitsNeeded = -8;
                this.value += this.ReadByte();
            }
        }

        return false;
    }

    /// <summary>
    /// Validates the stop bit and zero padding following a terminating entropy-coded value.
    /// </summary>
    /// <exception cref="InvalidImageContentException">The entropy substream has an invalid stop or alignment bit.</exception>
    public readonly void ValidateTerminationAlignment()
    {
        int alignmentShift = 8 + this.bitsNeeded;

        // CABAC refills whole bytes ahead of consumption. The stop bit therefore remains in the most recently
        // loaded byte, and bitsNeeded identifies its exact position without rewinding the arithmetic decoder.
        int alignmentPattern = (this.data[this.byteOffset - 1] << alignmentShift) & 0xFF;
        if (alignmentPattern != 0x80)
        {
            throw new InvalidImageContentException("The HEVC CABAC substream has invalid termination alignment.");
        }
    }

    /// <summary>
    /// Decodes equal-probability values while the arithmetic range is byte aligned.
    /// </summary>
    /// <param name="bitCount">The number of values to decode.</param>
    /// <returns>The decoded unsigned value.</returns>
    /// <exception cref="InvalidImageContentException">The entropy substream ends while loading the decoded value.</exception>
    private uint ReadAlignedBypassBits(int bitCount)
    {
        uint bins = 0;
        int remaining = bitCount;
        while (remaining > 0)
        {
            int binsToRead = Math.Min(remaining, 8);
            uint binMask = (1U << binsToRead) - 1;

            // With a range of 256 the high value bit is known to be zero, so the following bits can be copied
            // directly while preserving the same register refill schedule as individual bypass decisions.
            uint newBins = (this.value >> (15 - binsToRead)) & binMask;
            bins = (bins << binsToRead) | newBins;
            this.value = (this.value << binsToRead) & 0x7FFF;
            remaining -= binsToRead;
            this.bitsNeeded += binsToRead;
            if (this.bitsNeeded >= 0)
            {
                this.value |= (uint)this.ReadByte() << this.bitsNeeded;
                this.bitsNeeded -= 8;
            }
        }

        return bins;
    }

    /// <summary>
    /// Loads the next byte into the arithmetic decoder.
    /// </summary>
    /// <returns>The next entropy-substream byte.</returns>
    /// <exception cref="InvalidImageContentException">No byte remains in the bounded entropy substream.</exception>
    private byte ReadByte()
    {
        if ((uint)this.byteOffset >= (uint)this.data.Length)
        {
            throw new InvalidImageContentException("The HEVC CABAC substream is truncated.");
        }

        return this.data[this.byteOffset++];
    }
}

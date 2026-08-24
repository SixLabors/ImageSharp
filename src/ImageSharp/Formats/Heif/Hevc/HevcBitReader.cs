// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Reads fixed-width and Exp-Golomb HEVC syntax from a most-significant-bit-first byte span.
/// </summary>
internal ref struct HevcBitReader
{
    /// <summary>
    /// The complete raw byte sequence buffer.
    /// </summary>
    private readonly ReadOnlySpan<byte> data;

    /// <summary>
    /// The zero-based position of the next bit to read.
    /// </summary>
    private int bitPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcBitReader"/> struct.
    /// </summary>
    /// <param name="data">The bounded HEVC syntax bytes.</param>
    public HevcBitReader(ReadOnlySpan<byte> data)
    {
        this.data = data;
        this.bitPosition = 0;
    }

    /// <summary>
    /// Gets the zero-based position of the next bit to read.
    /// </summary>
    public readonly int BitPosition => this.bitPosition;

    /// <summary>
    /// Gets the number of unread bits in the bounded byte span.
    /// </summary>
    public readonly int BitsRemaining => (this.data.Length * 8) - this.bitPosition;

    /// <summary>
    /// Gets a value indicating whether the next bit begins a byte.
    /// </summary>
    public readonly bool IsByteAligned => (this.bitPosition & 7) == 0;

    /// <summary>
    /// Reads an unsigned fixed-width value in most-significant-bit-first order.
    /// </summary>
    /// <param name="bitCount">The number of bits to read.</param>
    /// <returns>The decoded unsigned value.</returns>
    /// <exception cref="InvalidImageContentException">
    /// The requested value extends beyond the bounded HEVC syntax.
    /// </exception>
    public uint ReadBits(int bitCount)
    {
        DebugGuard.MustBeBetweenOrEqualTo(bitCount, 0, 32, nameof(bitCount));
        if (bitCount > this.BitsRemaining)
        {
            throw new InvalidImageContentException("The HEVC bitstream is truncated.");
        }

        uint value = 0;
        int remaining = bitCount;
        while (remaining > 0)
        {
            // HEVC fixed-width syntax is MSB-first. Reading only the available portion of each byte keeps the
            // same operation valid for both aligned parameter fields and fields that straddle byte boundaries.
            int byteOffset = this.bitPosition >> 3;
            int bitOffset = this.bitPosition & 7;
            int bitsFromByte = Math.Min(remaining, 8 - bitOffset);
            int shift = 8 - bitOffset - bitsFromByte;
            uint mask = (1U << bitsFromByte) - 1;

            value = (value << bitsFromByte) | ((uint)(this.data[byteOffset] >> shift) & mask);
            this.bitPosition += bitsFromByte;
            remaining -= bitsFromByte;
        }

        return value;
    }

    /// <summary>
    /// Reads a one-bit HEVC flag.
    /// </summary>
    /// <returns><see langword="true"/> when the coded flag is one; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidImageContentException">The flag extends beyond the bounded HEVC syntax.</exception>
    public bool ReadFlag() => this.ReadBits(1) != 0;

    /// <summary>
    /// Determines whether unread syntax remains before the raw byte sequence payload trailing bits.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the unread bits contain syntax before the stop bit; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool HasMoreRbspData()
    {
        int bitsRemaining = this.BitsRemaining;
        if (bitsRemaining == 0)
        {
            return false;
        }

        if (bitsRemaining > 8)
        {
            return true;
        }

        int savedBitPosition = this.bitPosition;
        uint remainingValue = this.ReadBits(bitsRemaining);
        this.bitPosition = savedBitPosition;

        // At most one partial byte can contain only rbsp_stop_one_bit followed by alignment zeros.
        return remainingValue != 1U << (bitsRemaining - 1);
    }

    /// <summary>
    /// Reads an unsigned exponential-Golomb value.
    /// </summary>
    /// <returns>The decoded unsigned value.</returns>
    /// <exception cref="InvalidImageContentException">
    /// The code is truncated or exceeds the range of a 32-bit unsigned integer.
    /// </exception>
    public uint ReadUnsignedExpGolomb()
    {
        int leadingZeroBits = 0;
        while (!this.ReadFlag())
        {
            leadingZeroBits++;
            if (leadingZeroBits > 32)
            {
                throw new InvalidImageContentException("The HEVC unsigned Exp-Golomb value exceeds 32 bits.");
            }
        }

        // In ue(v), the zero-prefix length selects an all-one basis and the equally wide suffix selects the
        // offset from that basis. Keeping those parts separate makes the 32-bit overflow boundary explicit.
        uint suffix = this.ReadBits(leadingZeroBits);
        if (leadingZeroBits == 32)
        {
            // Only an all-zero suffix fits after the 32-bit all-one basis.
            if (suffix != 0)
            {
                throw new InvalidImageContentException("The HEVC unsigned Exp-Golomb value exceeds 32 bits.");
            }

            return uint.MaxValue;
        }

        return ((1U << leadingZeroBits) - 1) + suffix;
    }

    /// <summary>
    /// Reads a signed exponential-Golomb value.
    /// </summary>
    /// <returns>The decoded signed value.</returns>
    /// <exception cref="InvalidImageContentException">
    /// The code is truncated or exceeds the range of a 32-bit signed integer.
    /// </exception>
    public int ReadSignedExpGolomb()
    {
        uint codeNumber = this.ReadUnsignedExpGolomb();

        // HEVC's se(v) mapping alternates positive and negative magnitudes: 0, 1, -1, 2, -2, and so on.
        if ((codeNumber & 1) == 0)
        {
            return -(int)(codeNumber >> 1);
        }

        ulong magnitude = ((ulong)codeNumber + 1) >> 1;
        if (magnitude > int.MaxValue)
        {
            throw new InvalidImageContentException("The HEVC signed Exp-Golomb value exceeds 32 bits.");
        }

        return (int)magnitude;
    }

    /// <summary>
    /// Reads the one-bit marker and zero padding that align slice data to the next byte boundary.
    /// </summary>
    /// <exception cref="InvalidImageContentException">
    /// The alignment marker is zero or any following alignment bit is nonzero.
    /// </exception>
    public void ReadByteAlignment()
    {
        if (!this.ReadFlag())
        {
            throw new InvalidImageContentException("The HEVC slice-header alignment marker is not set.");
        }

        while (!this.IsByteAligned)
        {
            if (this.ReadFlag())
            {
                throw new InvalidImageContentException("The HEVC slice header has a nonzero alignment bit.");
            }
        }
    }

    /// <summary>
    /// Reads and validates the stop bit and zero alignment bits that terminate an HEVC raw byte sequence payload.
    /// </summary>
    /// <exception cref="InvalidImageContentException">
    /// The trailing-bit pattern is truncated, malformed, or followed by additional data.
    /// </exception>
    public void ReadRbspTrailingBits()
    {
        // An RBSP ends with one stop bit followed only by zero bits up to the next byte boundary.
        if (!this.ReadFlag())
        {
            throw new InvalidImageContentException("The HEVC RBSP stop bit is not set.");
        }

        while (!this.IsByteAligned)
        {
            if (this.ReadFlag())
            {
                throw new InvalidImageContentException("The HEVC RBSP has a nonzero alignment bit.");
            }
        }

        // Each reader is bounded to one RBSP, so reaching alignment before the buffer end means the caller left
        // syntax unread or the NAL unit contains bytes beyond its normative terminator.
        if (this.BitsRemaining != 0)
        {
            throw new InvalidImageContentException("The HEVC RBSP contains unexpected trailing data.");
        }
    }
}

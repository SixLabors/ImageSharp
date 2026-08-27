// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Reads AV1 fixed-width and variable-length syntax from a most-significant-bit-first byte span.
/// </summary>
internal ref struct Av1BitStreamReader
{
    /// <summary>
    /// The complete encoded byte span.
    /// </summary>
    private readonly Span<byte> data;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BitStreamReader"/> struct.
    /// </summary>
    /// <param name="data">The encoded AV1 data.</param>
    public Av1BitStreamReader(Span<byte> data) => this.data = data;

    /// <summary>
    /// Gets the zero-based position of the next bit to read.
    /// </summary>
    public int BitPosition { get; private set; } = 0;

    /// <summary>
    /// Gets the number of bytes in the reader's buffer.
    /// </summary>
    public readonly int Length => this.data.Length;

    /// <summary>
    /// Moves the next read position to the beginning of the buffer.
    /// </summary>
    public void Reset() => this.BitPosition = 0;

    /// <summary>
    /// Advances the read position without interpreting the skipped bits.
    /// </summary>
    /// <param name="bitCount">The number of bits to skip.</param>
    public void Skip(int bitCount) => this.BitPosition += bitCount;

    /// <summary>
    /// Reads an unsigned fixed-width value in most-significant-bit-first order.
    /// </summary>
    /// <param name="bitCount">The number of bits to read.</param>
    /// <returns>The decoded unsigned value.</returns>
    public uint ReadLiteral(int bitCount)
    {
        DebugGuard.MustBeBetweenOrEqualTo(bitCount, 0, 32, nameof(bitCount));

        uint literal = 0;
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            literal |= this.ReadBit() << bit;
        }

        return literal;
    }

    /// <summary>
    /// Reads the next encoded bit.
    /// </summary>
    /// <returns>Zero or one.</returns>
    internal uint ReadBit()
    {
        int byteOffset = Av1Math.DivideBy8Floor(this.BitPosition);
        byte shift = (byte)(7 - Av1Math.Modulus8(this.BitPosition));
        this.BitPosition++;
        return (uint)((this.data[byteOffset] >> shift) & 0x01);
    }

    /// <summary>
    /// Reads the next encoded bit as a Boolean value.
    /// </summary>
    /// <returns><see langword="true"/> for one; otherwise, <see langword="false"/>.</returns>
    public bool ReadBoolean() => this.ReadLiteral(1) > 0;

    /// <summary>
    /// Reads an AV1 little-endian base-128 value from a byte-aligned position.
    /// </summary>
    /// <param name="length">Receives the number of encoded bytes consumed.</param>
    /// <returns>The decoded unsigned value.</returns>
    public ulong ReadLittleEndianBytes128(out int length)
    {
        DebugGuard.IsTrue((this.BitPosition & 0x07) == 0, $"Reading of Little Endian 128 value only allowed on byte alignment (offset {this.BitPosition}).");

        ulong value = 0;
        length = 0;
        for (int shift = 0; shift < 56; shift += 7)
        {
            uint leb128Byte = this.ReadLiteral(8);
            value |= (leb128Byte & 0x7FUL) << shift;
            length++;
            if ((leb128Byte & 0x80U) == 0)
            {
                return value;
            }
        }

        // AV1 limits unsigned LEB128 fields to eight bytes. A continuation bit in the eighth byte does not describe
        // another value byte; accepting it would move the following OBU header into the declared size field.
        throw new InvalidImageContentException("The AV1 LEB128 value is not terminated within eight bytes.");
    }

    /// <summary>
    /// Reads the AV1 unsigned-variable-length code.
    /// </summary>
    /// <returns>The decoded unsigned value.</returns>
    public uint ReadUnsignedVariableLength()
    {
        int leadingZerosCount = 0;
        while (leadingZerosCount < 32)
        {
            uint bit = this.ReadLiteral(1);
            if (bit == 1)
            {
                break;
            }

            leadingZerosCount++;
        }

        if (leadingZerosCount == 32)
        {
            return uint.MaxValue;
        }

        if (leadingZerosCount != 0)
        {
            uint basis = (1U << leadingZerosCount) - 1U;
            uint value = this.ReadLiteral(leadingZerosCount);
            return basis + value;
        }

        return 0;
    }

    /// <summary>
    /// Reads a value from an alphabet whose size is not a power of two.
    /// </summary>
    /// <param name="n">The number of symbols in the alphabet.</param>
    /// <returns>A decoded symbol in the range zero through <paramref name="n"/> minus one.</returns>
    public uint ReadNonSymmetric(uint n)
    {
        if (n <= 1)
        {
            return 0;
        }

        int w = (int)(Av1Math.FloorLog2(n) + 1);
        uint m = (uint)((1 << w) - n);
        uint v = this.ReadLiteral(w - 1);
        if (v < m)
        {
            return v;
        }

        return (v << 1) - m + this.ReadLiteral(1);
    }

    /// <summary>
    /// Reads a finite subexponential value recentered around a signed reference value.
    /// </summary>
    /// <param name="valueMagnitude">One greater than the maximum absolute value in the signed domain.</param>
    /// <param name="groupBitCount">The bit width of the first subexponential group.</param>
    /// <param name="reference">The signed reference value around which smaller codewords are concentrated.</param>
    /// <returns>A decoded value in the inclusive range from minus <paramref name="valueMagnitude"/> plus one through
    /// <paramref name="valueMagnitude"/> minus one.</returns>
    public int ReadSignedReferenceSubexponential(int valueMagnitude, int groupBitCount, int reference)
    {
        int shiftedReference = reference + valueMagnitude - 1;
        int scaledValueCount = (valueMagnitude << 1) - 1;
        return this.ReadReferenceSubexponential(scaledValueCount, groupBitCount, shiftedReference) - valueMagnitude + 1;
    }

    /// <summary>
    /// Reads a fixed-width two's-complement signed integer.
    /// </summary>
    /// <param name="n">The encoded bit width.</param>
    /// <returns>The sign-extended integer.</returns>
    public int ReadSignedFromUnsigned(int n)
    {
        int signedValue;
        uint value = this.ReadLiteral(n);
        uint signMask = 1U << (n - 1);
        if ((value & signMask) == signMask)
        {
            // The subtraction represents sign extension; widening first preserves the n=32 case.
            signedValue = (int)((long)value - (signMask << 1));
        }
        else
        {
            signedValue = (int)value;
        }

        return signedValue;
    }

    /// <summary>
    /// Reads a byte-aligned unsigned integer whose least-significant byte is encoded first.
    /// </summary>
    /// <param name="n">The number of bytes to read.</param>
    /// <returns>The decoded unsigned integer.</returns>
    public uint ReadLittleEndian(int n)
    {
        DebugGuard.IsTrue(Av1Math.Modulus8(this.BitPosition) == 0, "Reading of Little Endian value only allowed on byte alignment");

        uint t = 0;
        for (int i = 0; i < 8 * n; i += 8)
        {
            t += this.ReadLiteral(8) << i;
        }

        return t;
    }

    /// <summary>
    /// Gets a byte-aligned tile payload for entropy decoding and advances past it.
    /// </summary>
    /// <param name="tileDataSize">The tile payload length in bytes.</param>
    /// <returns>The tile payload span.</returns>
    public Span<byte> GetSymbolReader(int tileDataSize)
        => this.ReadBytes(tileDataSize);

    /// <summary>
    /// Gets the next byte-aligned portion of the encoded data and advances past it.
    /// </summary>
    /// <param name="byteCount">The number of bytes to read.</param>
    /// <returns>The requested bytes.</returns>
    public Span<byte> ReadBytes(int byteCount)
    {
        DebugGuard.IsTrue(Av1Math.Modulus8(this.BitPosition) == 0, "Byte spans must start on a byte boundary.");
        int byteOffset = Av1Math.DivideBy8Floor(this.BitPosition);
        if ((uint)byteOffset > (uint)this.data.Length || (uint)byteCount > (uint)(this.data.Length - byteOffset))
        {
            throw new InvalidImageContentException("The AV1 payload exceeds its declared data boundary.");
        }

        Span<byte> payload = this.data.Slice(byteOffset, byteCount);
        this.Skip(byteCount << 3);
        return payload;
    }

    /// <summary>
    /// Reads a finite subexponential value and inverse-recenters it around an unsigned reference value.
    /// </summary>
    /// <param name="valueCount">The number of values in the finite domain.</param>
    /// <param name="groupBitCount">The bit width of the first subexponential group.</param>
    /// <param name="reference">The reference value within the finite domain.</param>
    /// <returns>The decoded value in the range zero through <paramref name="valueCount"/> minus one.</returns>
    private int ReadReferenceSubexponential(int valueCount, int groupBitCount, int reference)
    {
        int value = this.ReadSubexponential(valueCount, groupBitCount);

        // Recentering enumerates values by increasing distance from the reference. References in the upper half use
        // the mirrored domain so the shorter side of the finite range always participates in the alternating mapping.
        if ((reference << 1) <= valueCount)
        {
            return InverseRecenter(reference, value);
        }

        return valueCount - 1 - InverseRecenter(valueCount - 1 - reference, value);
    }

    /// <summary>
    /// Reads one value from a finite subexponential code.
    /// </summary>
    /// <param name="valueCount">The number of values in the finite domain.</param>
    /// <param name="groupBitCount">The bit width of the first subexponential group.</param>
    /// <returns>The decoded zero-based value.</returns>
    private int ReadSubexponential(int valueCount, int groupBitCount)
    {
        int groupIndex = 0;
        int groupStart = 0;
        while (true)
        {
            // AV1 keeps the first two groups at width k and then doubles each following group. Once fewer than three
            // groups remain, the non-symmetric code consumes the exact finite tail without introducing unused values.
            int bitCount = groupIndex == 0 ? groupBitCount : groupBitCount + groupIndex - 1;
            int groupSize = 1 << bitCount;
            if (valueCount <= groupStart + (3 * groupSize))
            {
                return (int)this.ReadNonSymmetric((uint)(valueCount - groupStart)) + groupStart;
            }

            if (!this.ReadBoolean())
            {
                return (int)this.ReadLiteral(bitCount) + groupStart;
            }

            groupIndex++;
            groupStart += groupSize;
        }
    }

    /// <summary>
    /// Maps a nonnegative code value around a nonnegative reference value.
    /// </summary>
    /// <param name="reference">The recentering reference.</param>
    /// <param name="value">The coded nonnegative value.</param>
    /// <returns>The inverse-recentered value.</returns>
    private static int InverseRecenter(int reference, int value)
    {
        // Codes within twice the reference alternate above and below it: even values select the upper side and odd
        // values select the lower side. Larger codes lie beyond the lower-side range and map directly to the tail.
        if (value > (reference << 1))
        {
            return value;
        }

        return (value & 1) == 0 ? (value >> 1) + reference : reference - ((value + 1) >> 1);
    }
}

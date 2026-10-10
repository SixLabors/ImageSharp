// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Writes AV1 fixed-width and variable-length syntax to a caller-provided buffer.
/// </summary>
internal ref struct Av1BitStreamWriter
{
    /// <summary>
    /// The writable output buffer.
    /// </summary>
    private readonly Span<byte> span;

    /// <summary>
    /// The partially assembled output byte.
    /// </summary>
    private byte buffer = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BitStreamWriter"/> struct.
    /// </summary>
    /// <param name="span">The preallocated output buffer.</param>
    public Av1BitStreamWriter(Span<byte> span)
    {
        this.span = span;
    }

    /// <summary>
    /// Gets the zero-based position of the next output bit.
    /// </summary>
    public int BitPosition { get; private set; } = 0;

    /// <summary>
    /// Encodes an unsigned 32-bit value using little-endian base-128 bytes.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="span">The destination receiving up to five bytes.</param>
    /// <returns>The number of bytes written.</returns>
    public static int GetLittleEndianBytes128(uint value, Span<byte> span)
    {
        int length = 0;
        do
        {
            byte encodedByte = (byte)(value & 0x7fU);
            value >>= 7;
            if (value != 0)
            {
                encodedByte |= 0x80;
            }

            span[length++] = encodedByte;
        }
        while (value != 0);

        return length;
    }

    /// <summary>
    /// Stores a partially assembled byte and resets the position to zero, so the next write starts at the beginning of the buffer.
    /// </summary>
    public void Flush()
    {
        if (Av1Math.Modulus8(this.BitPosition) != 0)
        {
            this.WriteBuffer();
        }

        this.BitPosition = 0;
    }

    /// <summary>
    /// Writes an unsigned fixed-width value in most-significant-bit-first order.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="bitCount">The number of low-order bits to write.</param>
    public void WriteLiteral(uint value, int bitCount)
    {
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            this.WriteBit((byte)((value >> bit) & 0x1));
        }
    }

    /// <summary>
    /// Writes one Boolean bit.
    /// </summary>
    /// <param name="value">The Boolean value.</param>
    public void WriteBoolean(bool value)
    {
        byte boolByte = value ? (byte)1 : (byte)0;
        this.WriteBit(boolByte);
    }

    /// <summary>
    /// Writes a fixed-width signed integer in two's-complement form.
    /// </summary>
    /// <param name="signedValue">The signed value.</param>
    /// <param name="n">The encoded bit width.</param>
    public void WriteSignedFromUnsigned(int signedValue, int n)
    {
        ulong value = (ulong)signedValue;
        if (signedValue < 0)
        {
            value += 1UL << n;
        }

        this.WriteLiteral((uint)value, n);
    }

    /// <summary>
    /// Writes a value from an alphabet whose size is not a power of two.
    /// </summary>
    /// <param name="value">The symbol value.</param>
    /// <param name="numberOfSymbols">The number of symbols in the alphabet.</param>
    public void WriteNonSymmetric(uint value, uint numberOfSymbols)
    {
        if (numberOfSymbols <= 1)
        {
            return;
        }

        int w = (int)(Av1Math.FloorLog2(numberOfSymbols) + 1);
        uint m = (uint)((1 << w) - numberOfSymbols);
        if (value < m)
        {
            this.WriteLiteral(value, w - 1);
        }
        else
        {
            // Values from m upward use w bits, as the ns(n) syntax of AV1 defines. The (w - 1)-bit prefix is m plus half the offset from m. The last bit is the
            // low bit of that offset. The reader computes (prefix * 2) - m + bit, which gives the value again.
            uint offset = value - m;
            uint k = m + (offset >> 1);
            this.WriteLiteral(k, w - 1);
            this.WriteLiteral(offset & 1, 1);
        }
    }

    /// <summary>
    /// Writes a finite subexponential value recentered around a signed reference value.
    /// </summary>
    /// <param name="value">The signed value to write.</param>
    /// <param name="valueMagnitude">One greater than the maximum absolute value in the signed domain.</param>
    /// <param name="groupBitCount">The bit width of the first subexponential group.</param>
    /// <param name="reference">The signed reference value around which smaller codewords are concentrated.</param>
    public void WriteSignedReferenceSubexponential(int value, int valueMagnitude, int groupBitCount, int reference)
    {
        int shiftedReference = reference + valueMagnitude - 1;
        int shiftedValue = value + valueMagnitude - 1;
        int scaledValueCount = (valueMagnitude << 1) - 1;
        int recenteredValue = RecenterFiniteNonNegative(scaledValueCount, shiftedReference, shiftedValue);

        this.WriteSubexponential(recenteredValue, scaledValueCount, groupBitCount);
    }

    /// <summary>
    /// Writes one value with a finite sequence of code groups whose sizes grow exponentially.
    /// </summary>
    /// <param name="value">The zero-based value to write. It is less than <paramref name="valueCount"/>.</param>
    /// <param name="valueCount">The number of values in the finite domain.</param>
    /// <param name="groupBitCount">The bit width of the first subexponential group.</param>
    private void WriteSubexponential(int value, int valueCount, int groupBitCount)
    {
        int groupIndex = 0;
        int groupStart = 0;
        while (true)
        {
            // The first two groups keep the initial width. Later groups grow one bit at a time until the finite tail fits in three groups. Then the exact
            // non-symmetric code writes the tail.
            int bitCount = groupIndex == 0 ? groupBitCount : groupBitCount + groupIndex - 1;
            int groupSize = 1 << bitCount;
            if (valueCount <= groupStart + (3 * groupSize))
            {
                this.WriteNonSymmetric((uint)(value - groupStart), (uint)(valueCount - groupStart));

                return;
            }

            bool useLaterGroup = value >= groupStart + groupSize;
            this.WriteBoolean(useLaterGroup);
            if (!useLaterGroup)
            {
                this.WriteLiteral((uint)(value - groupStart), bitCount);
                return;
            }

            groupIndex++;
            groupStart += groupSize;
        }
    }

    /// <summary>
    /// Maps an unsigned value to increasing distance from a reference inside a finite domain.
    /// </summary>
    /// <param name="valueCount">The number of values in the finite domain.</param>
    /// <param name="reference">The reference value within the finite domain.</param>
    /// <param name="value">The value to map, within the finite domain.</param>
    /// <returns>The recentered code value in the range zero through <paramref name="valueCount"/> minus one.</returns>
    private static int RecenterFiniteNonNegative(int valueCount, int reference, int value)
    {
        // References in the upper half use the mirrored domain. Thus the shorter side of the finite range is the side that alternates.
        if ((reference << 1) <= valueCount)
        {
            return RecenterNonNegative(reference, value);
        }

        return RecenterNonNegative(valueCount - 1 - reference, valueCount - 1 - value);
    }

    /// <summary>
    /// Maps an unsigned value to alternating positions around a nonnegative reference.
    /// </summary>
    /// <param name="reference">The recentering reference.</param>
    /// <param name="value">The nonnegative value to map.</param>
    /// <returns>The recentered code value.</returns>
    private static int RecenterNonNegative(int reference, int value)
    {
        // Values up to twice the reference alternate. Values at or above the reference get even codes, and values below it get odd codes. Larger values lie
        // past the range of the lower side and keep their own value.
        if (value > (reference << 1))
        {
            return value;
        }

        return value >= reference
            ? (value - reference) << 1
            : ((reference - value) << 1) - 1;
    }

    /// <summary>
    /// Appends one bit to the partially assembled output byte.
    /// </summary>
    /// <param name="value">Zero or one.</param>
    private void WriteBit(byte value)
    {
        int bit = this.BitPosition & 0x07;
        this.buffer = (byte)(((value << (7 - bit)) & 0xff) | this.buffer);
        if (bit == 7)
        {
            this.WriteBuffer();
        }

        this.BitPosition++;
    }

    /// <summary>
    /// Stores the current output byte.
    /// </summary>
    private void WriteBuffer()
    {
        int wordPosition = Av1Math.DivideBy8Floor(this.BitPosition);
        this.span[wordPosition] = this.buffer;
        this.buffer = 0;
    }
}

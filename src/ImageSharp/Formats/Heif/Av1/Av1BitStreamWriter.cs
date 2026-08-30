// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Writes AV1 fixed-width and variable-length syntax to reusable expanding memory.
/// </summary>
internal ref struct Av1BitStreamWriter
{
    /// <summary>
    /// The number of bits in one output byte.
    /// </summary>
    private const int WordSize = 8;

    /// <summary>
    /// The expanding output allocation.
    /// </summary>
    private readonly AutoExpandingMemory<byte> memory;

    /// <summary>
    /// The current writable view over <see cref="memory"/>.
    /// </summary>
    private Span<byte> span;

    /// <summary>
    /// The final byte index that can be written without expanding <see cref="memory"/>.
    /// </summary>
    private int capacityTrigger;

    /// <summary>
    /// The partially assembled output byte.
    /// </summary>
    private byte buffer = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BitStreamWriter"/> struct.
    /// </summary>
    /// <param name="memory">The reusable expanding output allocation.</param>
    public Av1BitStreamWriter(AutoExpandingMemory<byte> memory)
    {
        this.memory = memory;
        this.span = memory.GetEntireSpan();
        this.capacityTrigger = memory.Capacity - 1;
    }

    /// <summary>
    /// Gets the zero-based position of the next output bit.
    /// </summary>
    public int BitPosition { get; private set; } = 0;

    /// <summary>
    /// Gets the current output capacity in bytes.
    /// </summary>
    public readonly int Capacity => this.memory.Capacity;

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
    /// Advances the output position, emitting the current byte whenever the skip crosses a byte boundary.
    /// </summary>
    /// <param name="bitCount">The number of bits to skip.</param>
    public void Skip(int bitCount)
    {
        this.BitPosition += bitCount;
        while (this.BitPosition >= WordSize)
        {
            this.BitPosition -= WordSize;
            this.WriteBuffer();
        }
    }

    /// <summary>
    /// Writes a partially assembled byte and resets the position for output-memory reuse.
    /// </summary>
    public void Flush()
    {
        if (Av1Math.Modulus8(this.BitPosition) != 0)
        {
            // Flush a partial byte also.
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
    /// Writes an unsigned 32-bit value using little-endian base-128 bytes.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteLittleEndianBytes128(uint value)
    {
        int wordPosition = this.BitPosition >> 3;
        const int maximumEncodedLength = 5;
        if (this.span.Length - wordPosition < maximumEncodedLength)
        {
            this.memory.GetSpan(wordPosition + maximumEncodedLength);
            this.span = this.memory.GetEntireSpan();
            this.capacityTrigger = this.span.Length - 1;
        }

        int bytesWritten = GetLittleEndianBytes128(value, this.span[wordPosition..]);
        this.BitPosition += bytesWritten << 3;
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
            uint extraBit = ((value + m) >> 1) - value;
            uint k = (value + m - extraBit) >> 1;
            this.WriteLiteral(k, w - 1);
            this.WriteLiteral(extraBit, 1);
        }
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
    /// Writes an unsigned integer with its least-significant byte first.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="n">The number of bytes to write.</param>
    public void WriteLittleEndian(uint value, int n)
    {
        DebugGuard.IsTrue(Av1Math.Modulus8(this.BitPosition) == 0, "Writing of Little Endian value only allowed on byte alignment");

        uint t = value;
        for (int i = 0; i < n; i++)
        {
            this.WriteLiteral(t & 0xff, 8);
            t >>= 8;
        }
    }

    /// <summary>
    /// Writes a byte-aligned entropy-coded tile payload.
    /// </summary>
    /// <param name="tileData">The tile payload.</param>
    public void WriteBlob(ReadOnlySpan<byte> tileData)
    {
        DebugGuard.IsTrue(Av1Math.Modulus8(this.BitPosition) == 0, "Writing of Tile Data only allowed on byte alignment");

        int wordPosition = this.BitPosition >> 3;
        if (this.span.Length <= wordPosition + tileData.Length)
        {
            this.memory.GetSpan(wordPosition + tileData.Length);
            this.span = this.memory.GetEntireSpan();
        }

        tileData.CopyTo(this.span[wordPosition..]);
        this.BitPosition += tileData.Length << 3;
    }

    /// <summary>
    /// Stores the current output byte, expanding the allocation when necessary.
    /// </summary>
    private void WriteBuffer()
    {
        int wordPosition = Av1Math.DivideBy8Floor(this.BitPosition);
        if (wordPosition > this.capacityTrigger)
        {
            // Expand the memory allocation.
            this.memory.GetSpan(wordPosition + 1);
            this.span = this.memory.GetEntireSpan();
            this.capacityTrigger = this.span.Length - 1;
        }

        this.span[wordPosition] = this.buffer;
        this.buffer = 0;
    }
}

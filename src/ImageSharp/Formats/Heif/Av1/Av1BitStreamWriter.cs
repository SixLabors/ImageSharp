// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal ref struct Av1BitStreamWriter
{
    private const int WordSize = 8;
    private readonly AutoExpandingMemory<byte> memory;
    private Span<byte> span;
    private int capacityTrigger;
    private byte buffer = 0;

    public Av1BitStreamWriter(AutoExpandingMemory<byte> memory)
    {
        this.memory = memory;
        this.span = memory.GetEntireSpan();
        this.capacityTrigger = memory.Capacity - 1;
    }

    public int BitPosition { get; private set; } = 0;

    public readonly int Capacity => this.memory.Capacity;

    public static int GetLittleEndianBytes128(uint value, Span<byte> span)
    {
        if (value < 0x80U)
        {
            span[0] = (byte)value;
            return 1;
        }
        else if (value < 0x8000U)
        {
            span[0] = (byte)((value & 0x7fU) | 0x80U);
            span[1] = (byte)((value >> 7) & 0xff);
            return 2;
        }
        else if (value < 0x800000U)
        {
            span[0] = (byte)((value & 0x7fU) | 0x80U);
            span[1] = (byte)((value >> 7) & 0xff);
            span[2] = (byte)((value >> 14) & 0xff);
            return 3;
        }
        else
        {
            throw new NotImplementedException("No such large values yet.");
        }
    }

    public void Skip(int bitCount)
    {
        this.BitPosition += bitCount;
        while (this.BitPosition >= WordSize)
        {
            this.BitPosition -= WordSize;
            this.WriteBuffer();
        }
    }

    public void Flush()
    {
        if (Av1Math.Modulus8(this.BitPosition) != 0)
        {
            // Flush a partial byte also.
            this.WriteBuffer();
        }

        this.BitPosition = 0;
    }

    public void WriteLiteral(uint value, int bitCount)
    {
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            this.WriteBit((byte)((value >> bit) & 0x1));
        }
    }

    internal void WriteBoolean(bool value)
    {
        byte boolByte = value ? (byte)1 : (byte)0;
        this.WriteBit(boolByte);
    }

    public void WriteSignedFromUnsigned(int signedValue, int n)
    {
        // See section 4.10.6 of the AV1-Specification
        ulong value = (ulong)signedValue;
        if (signedValue < 0)
        {
            value += 1UL << n;
        }

        this.WriteLiteral((uint)value, n);
    }

    public void WriteLittleEndianBytes128(uint value)
    {
        int bytesWritten = GetLittleEndianBytes128(value, this.span.Slice(this.BitPosition >> 3));
        this.BitPosition += bytesWritten << 3;
    }

    internal void WriteNonSymmetric(uint value, uint numberOfSymbols)
    {
        // See section 4.10.7 of the AV1-Specification
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

    public void WriteLittleEndian(uint value, int n)
    {
        // See section 4.10.4 of the AV1-Specification
        DebugGuard.IsTrue(Av1Math.Modulus8(this.BitPosition) == 0, "Writing of Little Endian value only allowed on byte alignment");

        uint t = value;
        for (int i = 0; i < n; i++)
        {
            this.WriteLiteral(t & 0xff, 8);
            t >>= 8;
        }
    }

    internal void WriteBlob(Span<byte> tileData)
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

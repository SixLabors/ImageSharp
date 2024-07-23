// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal ref struct Av1BitStreamWriter(Stream stream)
{
    private const int WordSize = 8;
    private readonly Stream stream = stream;
    private byte buffer = 0;
    private int bitOffset = 0;

    public readonly int BitPosition => (int)(this.stream.Position * WordSize) + this.bitOffset;

    public readonly int Length => (int)this.stream.Length;

    public void Skip(int bitCount)
    {
        this.bitOffset += bitCount;
        while (this.bitOffset >= WordSize)
        {
            this.bitOffset -= WordSize;
            this.stream.WriteByte(this.buffer);
            this.buffer = 0;
        }
    }

    public void Flush()
    {
        if (Av1Math.Modulus8(this.bitOffset) != 0)
        {
            // Flush a partial byte also.
            this.stream.WriteByte(this.buffer);
        }

        this.bitOffset = 0;
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
        if (value < 128)
        {
            this.WriteLiteral(value, 8);
        }
        else if (value < 0x8000U)
        {
            this.WriteLiteral((value & 0x7FU) | 0x80U, 8);
            this.WriteLiteral(value >> 7, 8);
        }
        else
        {
            throw new NotImplementedException("No such large values yet.");
        }
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
        this.buffer = (byte)(((value << (7 - this.bitOffset)) & 0xff) | this.buffer);
        this.bitOffset++;
        if (this.bitOffset == WordSize)
        {
            this.stream.WriteByte(this.buffer);
            this.buffer = 0;
            this.bitOffset = 0;
        }
    }
}

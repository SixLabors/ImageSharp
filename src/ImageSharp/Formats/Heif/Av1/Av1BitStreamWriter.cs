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
        this.stream.WriteByte(this.buffer);
        this.bitOffset = 0;
    }

    public void WriteLiteral(uint value, int bitCount)
    {
        int shift = 24;
        uint padded = value << ((32 - bitCount) - this.bitOffset);
        while ((bitCount + this.bitOffset) >= 8)
        {
            byte current = (byte)(((padded >> shift) & 0xff) | this.buffer);
            this.stream.WriteByte(current);
            shift -= 8;
            bitCount -= 8;
            this.buffer = 0;
            this.bitOffset = 0;
        }

        if (bitCount > 0)
        {
            this.buffer = (byte)(((padded >> shift) & 0xff) | this.buffer);
            this.bitOffset += bitCount;
        }
    }

    internal void WriteBoolean(bool value)
    {
        byte boolByte = value ? (byte)1 : (byte)0;
        this.buffer = (byte)(((boolByte << (7 - this.bitOffset)) & 0xff) | this.buffer);
        this.bitOffset++;
        if (this.bitOffset == WordSize)
        {
            this.stream.WriteByte(this.buffer);
            this.bitOffset = 0;
        }
    }

    public void WriteSignedFromUnsigned(int signedValue, int n)
    {
        // See section 4.10.6 of the AV1-Specification
        uint value = unchecked((uint)signedValue);
        this.WriteLiteral(value, n + 1);
    }

    public void WriteLittleEndianBytes128(uint value)
    {
        if (value < 128)
        {
            this.WriteLiteral(value, 8);
        }
        else if (value < 0x8000U)
        {
            this.WriteLiteral(value >> 7, 8);
            this.WriteLiteral(value & 0x80, 8);
        }
        else
        {
            throw new NotImplementedException("No such large values yet.");
        }
    }
}

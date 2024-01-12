// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal ref struct Av1BitStreamWriter(Stream stream)
{
    private const int WordSize = sizeof(byte) * 8;
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
        while (bitCount >= 8)
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

    internal void WriteBoolean(bool value) => this.WriteLiteral(value ? 1U : 0U, 1);
}

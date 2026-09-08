// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Jpeg;

internal sealed class JpegBitWriter
{
    private const int ChunkSize = 16 * 1024;

    private readonly Stream stream;
    private readonly byte[] buffer;

    private int pos;
    private ulong putBuffer;
    private int putBits;

    public JpegBitWriter(Stream stream)
    {
        this.stream = stream;
        this.buffer = new byte[ChunkSize];
        this.putBits = 64;
    }

    public bool Healthy { get; private set; } = true;

    private void SwapBuffer()
    {
        if (this.pos == 0)
        {
            return;
        }

        this.stream.Write(this.buffer, 0, this.pos);
        this.pos = 0;
    }

    public void Reserve(int nBytes)
    {
        if (this.pos + nBytes > this.buffer.Length)
        {
            this.SwapBuffer();
        }
    }

    private void EmitByte(int value)
    {
        this.buffer[this.pos++] = (byte)value;

        if (value == 0xff)
        {
            this.buffer[this.pos++] = 0;
        }
    }

    public void EmitMarker(int marker)
    {
        this.Reserve(2);

        this.buffer[this.pos++] = 0xff;
        this.buffer[this.pos++] = (byte)marker;
    }

    public void StoreBe64(ulong value)
    {
        this.buffer[this.pos++] = (byte)(value >> 56);
        this.buffer[this.pos++] = (byte)(value >> 48);
        this.buffer[this.pos++] = (byte)(value >> 40);
        this.buffer[this.pos++] = (byte)(value >> 32);
        this.buffer[this.pos++] = (byte)(value >> 24);
        this.buffer[this.pos++] = (byte)(value >> 16);
        this.buffer[this.pos++] = (byte)(value >> 8);
        this.buffer[this.pos++] = (byte)value;
    }

    public void DischargeBitBuffer(int nBits, ulong bits)
    {
        this.putBuffer |= bits >> -this.putBits;

        if (HasZeroByte(~this.putBuffer))
        {
            this.Reserve(16);

            this.EmitByte((int)(this.putBuffer >> 56));
            this.EmitByte((int)(this.putBuffer >> 48));
            this.EmitByte((int)(this.putBuffer >> 40));
            this.EmitByte((int)(this.putBuffer >> 32));
            this.EmitByte((int)(this.putBuffer >> 24));
            this.EmitByte((int)(this.putBuffer >> 16));
            this.EmitByte((int)(this.putBuffer >> 8));
            this.EmitByte((int)this.putBuffer);
        }
        else
        {
            this.Reserve(8);
            this.StoreBe64(this.putBuffer);
        }

        this.putBits += 64;
        this.putBuffer = bits << this.putBits;
    }

    public void WriteBits(int nBits, ulong bits)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nBits);

        this.putBits -= nBits;

        if (this.putBits < 0)
        {
            if (nBits > 64)
            {
                this.putBits += nBits;
                this.Healthy = false;
            }
            else
            {
                this.DischargeBitBuffer(nBits, bits);
            }
        }
        else
        {
            this.putBuffer |= bits << this.putBits;
        }
    }

    public bool JumpToByteBoundary(ref ReadOnlySpan<byte> padBits)
    {
        int nBits = this.putBits & 7;
        byte padPattern;

        if (padBits.IsEmpty)
        {
            padPattern = (byte)((1 << nBits) - 1);
        }
        else
        {
            padPattern = 0;
            byte danglingBits = 0;

            for (int i = 0; i < nBits; ++i)
            {
                if (padBits.IsEmpty)
                {
                    return false;
                }

                byte bit = padBits[0];
                padBits = padBits[1..];

                danglingBits |= bit;

                padPattern <<= 1;
                padPattern |= bit;
            }

            if ((danglingBits & ~1) != 0)
            {
                return false;
            }
        }

        this.Reserve(16);

        while (this.putBits <= 56)
        {
            int c = (int)(this.putBuffer >> 56);

            this.EmitByte(c);

            this.putBuffer <<= 8;
            this.putBits += 8;
        }

        if (this.putBits < 64)
        {
            int padMask = 0xff >> (64 - this.putBits);

            int c =
                ((int)(this.putBuffer >> 56) & ~padMask) |
                padPattern;

            this.EmitByte(c);
        }

        this.putBuffer = 0;
        this.putBits = 64;

        return true;
    }
}

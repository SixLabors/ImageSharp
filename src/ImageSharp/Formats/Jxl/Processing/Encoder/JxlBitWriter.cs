// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

internal sealed class JxlBitWriter(Stream stream)
{
    private const int BitsPerByte = 8;

    private bool isLimited;
    private ulong bitsLimit;

    private byte currentByte;
    private int bitsInCurrentByte;

    public long BitsWritten { get; private set; }

    public void Write(int nBits, int bits) => this.Write(nBits, (ulong)bits);

    public void Write(int nBits, ulong bits)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(nBits, 0, nameof(nBits));
        DebugGuard.MustBeLessThanOrEqualTo(nBits, 64, nameof(nBits));

        if (this.isLimited)
        {
            if ((ulong)nBits > this.bitsLimit)
            {
                throw new InvalidOperationException("Too many bits were written");
            }

            this.bitsLimit -= (ulong)nBits;
        }

        while (nBits > 0)
        {
            int bitsAvailable = BitsPerByte - this.bitsInCurrentByte;
            int count = Math.Min(nBits, bitsAvailable);

            ulong mask = count == 64
                ? ulong.MaxValue
                : (1UL << count) - 1;

            this.currentByte |= (byte)((bits & mask) << this.bitsInCurrentByte);

            bits >>= count;
            nBits -= count;
            this.bitsInCurrentByte += count;
            this.BitsWritten += count;

            if (this.bitsInCurrentByte == BitsPerByte)
            {
                stream.WriteByte(this.currentByte);
                this.currentByte = 0;
                this.bitsInCurrentByte = 0;
            }
        }
    }

    public bool WithMaxBits(ulong maxBits, Func<bool> func)
    {
        bool previousIsLimited = this.isLimited;
        ulong previousLimit = this.bitsLimit;

        this.isLimited = true;
        this.bitsLimit = maxBits;

        try
        {
            return func();
        }
        finally
        {
            this.isLimited = previousIsLimited;
            this.bitsLimit = previousLimit;
        }
    }

    public void Flush()
    {
        if (this.bitsInCurrentByte != 0)
        {
            stream.WriteByte(this.currentByte);
            this.currentByte = 0;
            this.bitsInCurrentByte = 0;
        }
    }
}

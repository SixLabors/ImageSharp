// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Represents a bitstream reader.
/// </summary>
internal class JxlBitReader(Stream stream)
{
    private ulong buffer;
    private uint bufferRemainingBits;
    private int pointer;

    /// <summary>
    /// Gets the total number of bits consumed.
    /// </summary>
    public long TotalBitsConsumed => ((long)this.pointer * 8) + (64 - this.bufferRemainingBits);

    /// <summary>
    /// Fetches a new buffer.
    /// </summary>
    private void RefillCore()
    {
        Span<byte> temp = stackalloc byte[8];
        int bytesRead = stream.Read(temp);
        if (bytesRead == 8)
        {
            this.buffer = BinaryPrimitives.ReadUInt64LittleEndian(temp);
            this.bufferRemainingBits = 64u;
            this.pointer += 8;
        }
        else
        {
            if (bytesRead == 0)
            {
                throw new EndOfStreamException();
            }

            ulong value = 0;
            for (int i = 0; i < bytesRead; i++)
            {
                value |= (ulong)temp[i] << (8 * i);
            }

            this.buffer = value;
            this.bufferRemainingBits = (uint)(bytesRead * 8);
            this.pointer += bytesRead;
        }
    }

    public void JumpToByteBoundary()
    {
        uint remainder = (uint)(this.TotalBitsConsumed % 8);

        if (remainder == 0)
        {
            return;
        }

        if (this.ReadBits32(8u - remainder) != 0)
        {
            throw new InvalidDataException("Non-zero padding bits");
        }
    }

    private void MaybeRefill()
    {
        if (this.bufferRemainingBits <= 0)
        {
            this.RefillCore();
        }
    }

    private ulong ReadBits64Core(uint n, bool peek = false)
    {
        DebugGuard.MustBeLessThanOrEqualTo(n, 64u, nameof(n));
        this.MaybeRefill();

        if (n <= this.bufferRemainingBits)
        {
            ulong result = this.buffer & ((1UL << (int)n) - 1);

            if (!peek)
            {
                this.buffer >>= (int)n;
                this.bufferRemainingBits -= n;
            }

            return result;
        }
        else
        {
            uint bitsFromCurrent = this.bufferRemainingBits;
            ulong part = this.buffer & ((1UL << (int)bitsFromCurrent) - 1);

            this.buffer >>= (int)bitsFromCurrent;
            this.bufferRemainingBits = 0;

            this.RefillCore();

            uint bitsFromNext = n - bitsFromCurrent;
            ulong nextPart = this.buffer & ((1UL << (int)bitsFromNext) - 1);

            if (!peek)
            {
                this.buffer >>= (int)bitsFromNext;
                this.bufferRemainingBits -= bitsFromNext;
            }

            return part | (nextPart << (int)bitsFromCurrent);
        }
    }

    private uint ReadBits32Core(uint n, bool peek = false)
    {
        DebugGuard.MustBeLessThanOrEqualTo(n, 32u, nameof(n));
        this.MaybeRefill();

        if (n <= this.bufferRemainingBits)
        {
            uint result = (uint)(this.buffer & ((1UL << (int)n) - 1));

            if (!peek)
            {
                this.buffer >>= (int)n;
                this.bufferRemainingBits -= n;
            }

            return result;
        }
        else
        {
            uint bitsFromCurrent = this.bufferRemainingBits;
            uint part = (uint)(this.buffer & ((1UL << (int)bitsFromCurrent) - 1));

            this.buffer >>= (int)bitsFromCurrent;
            this.bufferRemainingBits = 0;

            this.RefillCore();

            uint bitsFromNext = n - bitsFromCurrent;
            uint nextPart = (uint)(this.buffer & ((1UL << (int)bitsFromNext) - 1));

            if (!peek)
            {
                this.buffer >>= (int)bitsFromNext;
                this.bufferRemainingBits -= bitsFromNext;
            }

            return part | (nextPart << (int)bitsFromCurrent);
        }
    }

    public virtual uint ReadBits32(uint bits) => this.ReadBits32Core(bits, peek: false);

    public virtual uint PeekBits32(uint bits) => this.ReadBits32Core(bits, peek: true);

    public virtual void SkipBits32(uint bits) => _ = this.ReadBits32(bits);

    public virtual ulong ReadBits64(ulong bits) => this.ReadBits64Core((uint)bits, peek: false);

    public virtual ulong PeekBits64(ulong bits) => this.ReadBits64Core((uint)bits, peek: true);

    public virtual void SkipBits64(ulong bits) => _ = this.ReadBits64(bits);

    public virtual bool ReadBoolean() => this.ReadBits32Core(1, peek: false) == 1;
}

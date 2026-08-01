// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Represents a bitstream reader.
/// </summary>
internal sealed class JxlBitReader(ReadOnlyMemory<byte> bytes)
{
    private ulong buffer;
    private uint bufferRemainingBits;
    private int pointer;

    /// <summary>
    /// Gets a value indicating whether this marks an end of stream.
    /// </summary>
    public bool IsEndOfStream { get; private set; }

    /// <summary>
    /// Gets the total number of bits consumed.
    /// </summary>
    public long TotalBitsConsumed => ((long)this.pointer * 8) + (64 - this.bufferRemainingBits);

    /// <summary>
    /// Fetches a new buffer.
    /// </summary>
    private void RefillCore()
    {
        ReadOnlySpan<byte> samplesSpan = bytes.Span;

        int remaining = samplesSpan.Length - this.pointer;
        if (remaining <= 0)
        {
            // we don't have any more data... mark an end of stream
            this.buffer = 0;
            this.bufferRemainingBits = 0;
            this.IsEndOfStream = true;
            return;
        }

        if (remaining >= 8)
        {
            this.buffer = BinaryPrimitives.ReadUInt64LittleEndian(samplesSpan[this.pointer..]);
            this.bufferRemainingBits = 64u;
            this.pointer += 8;
        }
        else
        {
            ulong value = 0;
            for (int i = 0; i < remaining; i++)
            {
                value |= (ulong)samplesSpan[this.pointer + i] << (8 * i);
            }

            this.buffer = value;
            this.bufferRemainingBits = (uint)(remaining * 8);
            this.pointer += remaining;
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

        if (this.IsEndOfStream)
        {
            JxlThrowHelper.ThrowEndOfStream();
        }

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

        if (this.IsEndOfStream)
        {
            JxlThrowHelper.ThrowEndOfStream();
        }

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

    public uint ReadBits32(uint bits) => this.ReadBits32Core(bits, peek: false);

    public uint PeekBits32(uint bits) => this.ReadBits32Core(bits, peek: true);

    public void SkipBits32(uint bits) => _ = this.ReadBits32(bits);

    public ulong ReadBits64(ulong bits) => this.ReadBits64Core((uint)bits, peek: false);

    public ulong PeekBits64(ulong bits) => this.ReadBits64Core((uint)bits, peek: true);

    public void SkipBits64(ulong bits) => _ = this.ReadBits64(bits);

    public bool ReadBoolean() => this.ReadBits32Core(1, peek: false) == 1;
}

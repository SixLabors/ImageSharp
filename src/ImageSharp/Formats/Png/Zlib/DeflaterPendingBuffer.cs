// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Png.Zlib
{
    /// <summary>
    /// Stores pending data for writing data to the Deflater.
    /// </summary>
    internal sealed unsafe class DeflaterPendingBuffer : IDisposable
    {
        private readonly byte[] buffer;
        private readonly byte* pinnedBuffer;
        private IManagedByteBuffer bufferMemoryOwner;
        private MemoryHandle bufferMemoryHandle;

        private int start;
        private int end;
        private uint bits;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflaterPendingBuffer"/> class.
        /// </summary>
        /// <param name="memoryAllocator">The memory allocator to use for buffer allocations.</param>
        public DeflaterPendingBuffer(MemoryAllocator memoryAllocator)
        {
            this.bufferMemoryOwner = memoryAllocator.AllocateManagedByteBuffer(DeflaterConstants.PENDING_BUF_SIZE);
            this.buffer = this.bufferMemoryOwner.Array;
            this.bufferMemoryHandle = this.bufferMemoryOwner.Memory.Pin();
            this.pinnedBuffer = (byte*)this.bufferMemoryHandle.Pointer;
        }

        /// <summary>
        /// Gets the number of bits written to the buffer.
        /// </summary>
        public int BitCount { get; private set; }

        /// <summary>
        /// Gets a value indicating whether indicates the buffer has been flushed.
        /// </summary>
        public bool IsFlushed => this.end == 0;

        /// <summary>
        /// Clear internal state/buffers.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Reset() => this.start = this.end = this.BitCount = 0;

        /// <summary>
        /// Write a short value to buffer LSB first.
        /// </summary>
        /// <param name="value">The value to write.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void WriteShort(int value)
        {
            byte* pinned = this.pinnedBuffer;
            pinned[this.end++] = unchecked((byte)value);
            pinned[this.end++] = unchecked((byte)(value >> 8));
        }

        /// <summary>
        /// Write a block of data to the internal buffer.
        /// </summary>
        /// <param name="block">The data to write.</param>
        /// <param name="offset">The offset of first byte to write.</param>
        /// <param name="length">The number of bytes to write.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void WriteBlock(byte[] block, int offset, int length)
        {
            Unsafe.CopyBlockUnaligned(ref this.buffer[this.end], ref block[offset], unchecked((uint)length));
            this.end += length;
        }

        /// <summary>
        /// Aligns internal buffer on a byte boundary.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void AlignToByte()
        {
            if (this.BitCount > 0)
            {
                byte* pinned = this.pinnedBuffer;
                pinned[this.end++] = unchecked((byte)this.bits);
                if (this.BitCount > 8)
                {
                    pinned[this.end++] = unchecked((byte)(this.bits >> 8));
                }
            }

            this.bits = 0;
            this.BitCount = 0;
        }

        /// <summary>
        /// Write bits to internal buffer
        /// </summary>
        /// <param name="b">source of bits</param>
        /// <param name="count">number of bits to write</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void WriteBits(int b, int count)
        {
            this.bits |= (uint)(b << this.BitCount);
            this.BitCount += count;
            if (this.BitCount >= 16)
            {
                byte* pinned = this.pinnedBuffer;
                pinned[this.end++] = unchecked((byte)this.bits);
                pinned[this.end++] = unchecked((byte)(this.bits >> 8));
                this.bits >>= 16;
                this.BitCount -= 16;
            }
        }

        /// <summary>
        /// Write a short value to internal buffer most significant byte first
        /// </summary>
        /// <param name="value">The value to write</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void WriteShortMSB(int value)
        {
            byte* pinned = this.pinnedBuffer;
            pinned[this.end++] = unchecked((byte)(value >> 8));
            pinned[this.end++] = unchecked((byte)value);
        }

        /// <summary>
        /// Flushes the pending buffer into the given output array.
        /// If the output array is to small, only a partial flush is done.
        /// </summary>
        /// <param name="output">The output array.</param>
        /// <param name="offset">The offset into output array.</param>
        /// <param name="length">The maximum number of bytes to store.</param>
        /// <returns>The number of bytes flushed.</returns>
        public int Flush(byte[] output, int offset, int length)
        {
            if (this.BitCount >= 8)
            {
                this.pinnedBuffer[this.end++] = unchecked((byte)this.bits);
                this.bits >>= 8;
                this.BitCount -= 8;
            }

            if (length > this.end - this.start)
            {
                length = this.end - this.start;

                Unsafe.CopyBlockUnaligned(ref output[offset], ref this.buffer[this.start], unchecked((uint)length));
                this.start = 0;
                this.end = 0;
            }
            else
            {
                Unsafe.CopyBlockUnaligned(ref output[offset], ref this.buffer[this.start], unchecked((uint)length));
                this.start += length;
            }

            return length;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.bufferMemoryHandle.Dispose();
                this.bufferMemoryOwner.Dispose();
                this.bufferMemoryOwner = null;
                this.isDisposed = true;
            }
        }
    }
}

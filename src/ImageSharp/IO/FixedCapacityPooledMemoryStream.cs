// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// A memory stream constructed from a pooled buffer of known length.
    /// </summary>
    internal sealed class FixedCapacityPooledMemoryStream : MemoryStream
    {
        private readonly IManagedByteBuffer buffer;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedCapacityPooledMemoryStream"/> class.
        /// </summary>
        /// <param name="length">The length of the stream buffer to rent.</param>
        /// <param name="allocator">The allocator to rent the buffer from.</param>
        public FixedCapacityPooledMemoryStream(long length, MemoryAllocator allocator)
            : this(RentBuffer(length, allocator)) => this.Length = length;

        private FixedCapacityPooledMemoryStream(IManagedByteBuffer buffer)
            : base(buffer.Array) => this.buffer = buffer;

        /// <inheritdoc/>
        public override long Length { get; }

        /// <inheritdoc/>
        public override bool TryGetBuffer(out ArraySegment<byte> buffer)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }

            buffer = new ArraySegment<byte>(this.buffer.Array, 0, this.buffer.Length());
            return true;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;

                if (disposing)
                {
                    this.buffer.Dispose();
                }

                base.Dispose(disposing);
            }
        }

        // In the extrememly unlikely event someone ever gives us a stream
        // with length longer than int.MaxValue then we'll use something else.
        private static IManagedByteBuffer RentBuffer(long length, MemoryAllocator allocator)
        {
            Guard.MustBeBetweenOrEqualTo(length, 0, int.MaxValue, nameof(length));
            return allocator.AllocateManagedByteBuffer((int)length);
        }
    }
}

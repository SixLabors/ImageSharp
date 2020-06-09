// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.IO;

namespace SixLabors.ImageSharp.IO
{
    /// <summary>
    /// A memory stream constructed from a pooled buffer of known length.
    /// </summary>
    internal sealed class FixedCapacityPooledMemoryStream : MemoryStream
    {
        private readonly byte[] buffer;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FixedCapacityPooledMemoryStream"/> class.
        /// </summary>
        /// <param name="length">The length of the stream buffer to rent.</param>
        public FixedCapacityPooledMemoryStream(long length)
            : this(RentBuffer(length)) => this.Length = length;

        private FixedCapacityPooledMemoryStream(byte[] buffer)
            : base(buffer) => this.buffer = buffer;

        /// <inheritdoc/>
        public override long Length { get; }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;

                if (disposing)
                {
                    ArrayPool<byte>.Shared.Return(this.buffer);
                }

                base.Dispose(disposing);
            }
        }

        // In the extrememly unlikely event someone ever gives us a stream
        // with length longer than int.MaxValue then we'll use something else.
        private static byte[] RentBuffer(long length) => ArrayPool<byte>.Shared.Rent((int)length);
    }
}

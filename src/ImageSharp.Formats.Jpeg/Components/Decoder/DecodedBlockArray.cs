// <copyright file="DecodedBlockArray.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Buffers;

    /// <summary>
    /// Because <see cref="System.Array.Length"/> has no information for rented arrays,
    /// we need to store the count and the buffer separately when storing pooled <see cref="DecodedBlock"/> arrays.
    /// </summary>
    internal struct DecodedBlockArray : IDisposable
    {
        /// <summary>
        /// The <see cref="ArrayPool{T}"/> used to pool data in <see cref="JpegDecoderCore.DecodedBlocks"/>.
        /// Should always clean arrays when returning!
        /// </summary>
        private static readonly ArrayPool<DecodedBlock> ArrayPool = ArrayPool<DecodedBlock>.Create();

        /// <summary>
        /// Initializes a new instance of the <see cref="DecodedBlockArray"/> struct. Rents a buffer.
        /// </summary>
        /// <param name="count">The number of valid <see cref="DecodedBlock"/>-s</param>
        public DecodedBlockArray(int count)
        {
            this.Count = count;
            this.Buffer = ArrayPool.Rent(count);
        }

        /// <summary>
        /// Gets the number of actual <see cref="DecodedBlock"/>-s inside <see cref="Buffer"/>
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets the rented buffer.
        /// </summary>
        public DecodedBlock[] Buffer { get; private set; }

        /// <summary>
        /// Returns the rented buffer to the pool.
        /// </summary>
        public void Dispose()
        {
            if (this.Buffer != null)
            {
                ArrayPool.Return(this.Buffer, true);
                this.Buffer = null;
            }
        }
    }
}
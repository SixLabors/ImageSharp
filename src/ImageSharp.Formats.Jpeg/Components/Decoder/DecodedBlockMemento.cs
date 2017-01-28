// <copyright file="DecodedBlockMemento.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Buffers;

    /// <summary>
    /// A structure to store unprocessed <see cref="Block8x8F"/> instances and their coordinates while scanning the image.
    /// </summary>
    internal struct DecodedBlockMemento
    {
        /// <summary>
        /// A value indicating whether the <see cref="DecodedBlockMemento"/> instance is initialized.
        /// </summary>
        public bool Initialized;

        /// <summary>
        /// X coordinate of the current block, in units of 8x8. (The third block in the first row has (bx, by) = (2, 0))
        /// </summary>
        public int Bx;

        /// <summary>
        /// Y coordinate of the current block, in units of 8x8. (The third block in the first row has (bx, by) = (2, 0))
        /// </summary>
        public int By;

        /// <summary>
        /// The <see cref="Block8x8F"/>
        /// </summary>
        public Block8x8F Block;

        /// <summary>
        /// Store the block data into a <see cref="DecodedBlockMemento"/> at the given index of an <see cref="DecodedBlockMemento.Array"/>.
        /// </summary>
        /// <param name="blockArray">The array <see cref="DecodedBlockMemento.Array"/></param>
        /// <param name="index">The index in the array</param>
        /// <param name="bx">X coordinate of the block</param>
        /// <param name="by">Y coordinate of the block</param>
        /// <param name="block">The <see cref="Block8x8F"/></param>
        public static void Store(ref DecodedBlockMemento.Array blockArray, int index, int bx, int by, ref Block8x8F block)
        {
            if (index >= blockArray.Count)
            {
                throw new IndexOutOfRangeException("Block index is out of range in DecodedBlockMemento.Store()!");
            }

            blockArray.Buffer[index].Initialized = true;
            blockArray.Buffer[index].Bx = bx;
            blockArray.Buffer[index].By = by;
            blockArray.Buffer[index].Block = block;
        }

        /// <summary>
        /// Because <see cref="System.Array.Length"/> has no information for rented arrays, we need to store the count and the buffer separately.
        /// </summary>
        public struct Array : IDisposable
        {
            /// <summary>
            /// The <see cref="ArrayPool{T}"/> used to pool data in <see cref="JpegDecoderCore.DecodedBlocks"/>.
            /// Should always clean arrays when returning!
            /// </summary>
            private static readonly ArrayPool<DecodedBlockMemento> ArrayPool = ArrayPool<DecodedBlockMemento>.Create();

            /// <summary>
            /// Initializes a new instance of the <see cref="Array"/> struct. Rents a buffer.
            /// </summary>
            /// <param name="count">The number of valid <see cref="DecodedBlockMemento"/>-s</param>
            public Array(int count)
            {
                this.Count = count;
                this.Buffer = ArrayPool.Rent(count);
            }

            /// <summary>
            /// Gets the number of actual <see cref="DecodedBlockMemento"/>-s inside <see cref="Buffer"/>
            /// </summary>
            public int Count { get; }

            /// <summary>
            /// Gets the rented buffer.
            /// </summary>
            public DecodedBlockMemento[] Buffer { get; private set; }

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
}
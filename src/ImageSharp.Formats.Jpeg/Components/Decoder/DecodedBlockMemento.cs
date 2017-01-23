// <copyright file="DecodedBlockMemento.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
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
        /// The <see cref="ArrayPool{T}"/> used to pool data in <see cref="JpegDecoderCore.DecodedBlocks"/>.
        /// Should always clean arrays when returning!
        /// </summary>
        private static readonly ArrayPool<DecodedBlockMemento> ArrayPool = ArrayPool<DecodedBlockMemento>.Create();

        /// <summary>
        /// Rent an array of <see cref="DecodedBlockMemento"/>-s from the pool.
        /// </summary>
        /// <param name="size">The requested array size</param>
        /// <returns>An array of <see cref="DecodedBlockMemento"/>-s</returns>
        public static DecodedBlockMemento[] RentArray(int size)
        {
            return ArrayPool.Rent(size);
        }

        /// <summary>
        /// Returns the <see cref="DecodedBlockMemento"/> array to the pool.
        /// </summary>
        /// <param name="blockArray">The <see cref="DecodedBlockMemento"/> array</param>
        public static void ReturnArray(DecodedBlockMemento[] blockArray)
        {
            ArrayPool.Return(blockArray, true);
        }

        /// <summary>
        /// Store the block data into a <see cref="DecodedBlockMemento"/> at the given index.
        /// </summary>
        /// <param name="blockArray">The array of <see cref="DecodedBlockMemento"/></param>
        /// <param name="index">The index in the array</param>
        /// <param name="bx">X coordinate of the block</param>
        /// <param name="by">Y coordinate of the block</param>
        /// <param name="block">The <see cref="Block8x8F"/></param>
        public static void Store(DecodedBlockMemento[] blockArray, int index, int bx, int by, ref Block8x8F block)
        {
            blockArray[index].Initialized = true;
            blockArray[index].Bx = bx;
            blockArray[index].By = by;
            blockArray[index].Block = block;
        }
    }
}
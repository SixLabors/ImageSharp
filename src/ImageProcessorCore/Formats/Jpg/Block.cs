// <copyright file="Block.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Represents an 8x8 block of coefficients to transform and encode.
    /// </summary>
    internal class Block
    {
        /// <summary>
        /// Gets the size of the block.
        /// </summary>
        public const int BlockSize = 64;

        /// <summary>
        /// The array of block data.
        /// </summary>
        private readonly int[] data;

        /// <summary>
        /// Initializes a new instance of the <see cref="Block"/> class.
        /// </summary>
        public Block()
        {
            this.data = new int[BlockSize];
        }

        /// <summary>
        /// Gets the pixel data at the given block index.
        /// </summary>
        /// <param name="index">The index of the data to return.</param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        public int this[int index]
        {
            get { return this.data[index]; }
            set { this.data[index] = value; }
        }
    }
}

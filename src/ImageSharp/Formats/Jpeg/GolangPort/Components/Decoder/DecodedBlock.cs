// <copyright file="DecodedBlock.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System;

    /// <summary>
    /// A structure to store unprocessed <see cref="Block8x8F"/> instances and their coordinates while scanning the image.
    /// The <see cref="Block"/> is present in a "raw" decoded frequency-domain form.
    /// We need to apply IDCT and unzigging to transform them into color-space blocks.
    /// </summary>
    internal struct DecodedBlock
    {
        /// <summary>
        /// A value indicating whether the <see cref="DecodedBlock"/> instance is initialized.
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
        /// Store the block data into a <see cref="DecodedBlock"/>
        /// </summary>
        /// <param name="bx">X coordinate of the block</param>
        /// <param name="by">Y coordinate of the block</param>
        /// <param name="block">The <see cref="Block8x8F"/></param>
        public void SaveBlock(int bx, int by, ref Block8x8F block)
        {
            this.Bx = bx;
            this.By = by;
            this.Block = block;
        }
    }
}
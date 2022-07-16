// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Represents the Huffman tree.
    /// </summary>
    [DebuggerDisplay("TotalCount = {TotalCount}, Value = {Value}, Left = {PoolIndexLeft}, Right = {PoolIndexRight}")]
    internal struct HuffmanTree
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HuffmanTree"/> struct.
        /// </summary>
        /// <param name="other">The HuffmanTree to create an instance from.</param>
        private HuffmanTree(HuffmanTree other)
        {
            this.TotalCount = other.TotalCount;
            this.Value = other.Value;
            this.PoolIndexLeft = other.PoolIndexLeft;
            this.PoolIndexRight = other.PoolIndexRight;
        }

        /// <summary>
        /// Gets or sets the symbol frequency.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Gets or sets the symbol value.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Gets or sets the index for the left sub-tree.
        /// </summary>
        public int PoolIndexLeft { get; set; }

        /// <summary>
        /// Gets or sets the index for the right sub-tree.
        /// </summary>
        public int PoolIndexRight { get; set; }

        public static int Compare(HuffmanTree t1, HuffmanTree t2)
        {
            if (t1.TotalCount > t2.TotalCount)
            {
                return -1;
            }

            if (t1.TotalCount < t2.TotalCount)
            {
                return 1;
            }

            return t1.Value < t2.Value ? -1 : 1;
        }
    }
}

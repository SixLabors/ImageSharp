// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    /// <summary>
    /// Represents the Huffman tree.
    /// </summary>
    internal class HuffmanTree
    {
        /// <summary>
        /// Gets the symbol frequency.
        /// </summary>
        public int TotalCount { get; }

        /// <summary>
        /// Gets the symbol value.
        /// </summary>
        public int Value { get; }

        /// <summary>
        /// Gets the index for the left sub-tree.
        /// </summary>
        public int PoolIndexLeft { get; }

        /// <summary>
        /// Gets the index for the right sub-tree.
        /// </summary>
        public int PoolIndexRight { get; }
    }
}

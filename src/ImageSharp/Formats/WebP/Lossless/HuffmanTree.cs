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
            else if (t1.TotalCount < t2.TotalCount)
            {
                return 1;
            }
            else
            {
                return (t1.Value < t2.Value) ? -1 : 1;
            }
        }
    }
}

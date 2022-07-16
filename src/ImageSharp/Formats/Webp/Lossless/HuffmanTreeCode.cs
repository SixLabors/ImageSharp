// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Represents the tree codes (depth and bits array).
    /// </summary>
    internal struct HuffmanTreeCode
    {
        /// <summary>
        /// Gets or sets the number of symbols.
        /// </summary>
        public int NumSymbols { get; set; }

        /// <summary>
        /// Gets or sets the code lengths of the symbols.
        /// </summary>
        public byte[] CodeLengths { get; set; }

        /// <summary>
        /// Gets or sets the symbol Codes.
        /// </summary>
        public short[] Codes { get; set; }
    }
}

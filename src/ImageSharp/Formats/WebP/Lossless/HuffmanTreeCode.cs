// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP.Lossless
{
    /// <summary>
    /// Represents the tree codes (depth and bits array).
    /// </summary>
    internal class HuffmanTreeCode
    {
        /// <summary>
        /// Gets the number of symbols.
        /// </summary>
        public int NumSymbols { get; }

        /// <summary>
        /// Gets the code lengths of the symbols.
        /// </summary>
        public byte[] CodeLengths { get; }

        /// <summary>
        /// Gets the symbol Codes.
        /// </summary>
        public short[] Codes { get; }
    }
}

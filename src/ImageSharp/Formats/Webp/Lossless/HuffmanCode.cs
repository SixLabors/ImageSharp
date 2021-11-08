// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// A classic way to do entropy coding where a smaller number of bits are used for more frequent codes.
    /// </summary>
    [DebuggerDisplay("BitsUsed: {BitsUsed}, Value: {Value}")]
    internal class HuffmanCode
    {
        /// <summary>
        /// Gets or sets the number of bits used for this symbol.
        /// </summary>
        public int BitsUsed { get; set; }

        /// <summary>
        /// Gets or sets the symbol value or table offset.
        /// </summary>
        public uint Value { get; set; }
    }
}

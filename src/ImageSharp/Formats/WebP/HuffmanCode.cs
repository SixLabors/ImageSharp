// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal class HuffmanCode
    {
        /// <summary>
        /// Gets or sets the number of bits used for this symbol.
        /// </summary>
        public int BitsUsed { get; set; }

        /// <summary>
        /// Gets or sets the symbol value or table offset.
        /// </summary>
        public int Value { get; set; }
    }
}

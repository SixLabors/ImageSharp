// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Five Huffman codes are used at each meta code.
    /// </summary>
    public enum HuffIndex : int
    {
        /// <summary>
        /// Green + length prefix codes + color cache codes.
        /// </summary>
        Green = 0,

        /// <summary>
        /// Red.
        /// </summary>
        Red = 1,

        /// <summary>
        /// Blue.
        /// </summary>
        Blue = 2,

        /// <summary>
        /// Alpha.
        /// </summary>
        Alpha = 3,

        /// <summary>
        /// Distance prefix codes.
        /// </summary>
        Dist = 4
    }
}

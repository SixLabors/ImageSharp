// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossless
{
    /// <summary>
    /// Five Huffman codes are used at each meta code.
    /// </summary>
    internal static class HuffIndex
    {
        /// <summary>
        /// Green + length prefix codes + color cache codes.
        /// </summary>
        public const int Green = 0;

        /// <summary>
        /// Red.
        /// </summary>
        public const int Red = 1;

        /// <summary>
        /// Blue.
        /// </summary>
        public const int Blue = 2;

        /// <summary>
        /// Alpha.
        /// </summary>
        public const int Alpha = 3;

        /// <summary>
        /// Distance prefix codes.
        /// </summary>
        public const int Dist = 4;
    }
}

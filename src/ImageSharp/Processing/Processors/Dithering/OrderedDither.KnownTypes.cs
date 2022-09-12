// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <content>
    /// An ordered dithering matrix with equal sides of arbitrary length
    /// </content>
    public readonly partial struct OrderedDither
    {
        /// <summary>
        /// Applies order dithering using the 2x2 Bayer dithering matrix.
        /// </summary>
        public static readonly OrderedDither Bayer2x2 = new(2);

        /// <summary>
        /// Applies order dithering using the 4x4 Bayer dithering matrix.
        /// </summary>
        public static readonly OrderedDither Bayer4x4 = new(4);

        /// <summary>
        /// Applies order dithering using the 8x8 Bayer dithering matrix.
        /// </summary>
        public static readonly OrderedDither Bayer8x8 = new(8);

        /// <summary>
        /// Applies order dithering using the 16x16 Bayer dithering matrix.
        /// </summary>
        public static readonly OrderedDither Bayer16x16 = new(16);

        /// <summary>
        /// Applies order dithering using the 3x3 ordered dithering matrix.
        /// </summary>
        public static readonly OrderedDither Ordered3x3 = new(3);
    }
}

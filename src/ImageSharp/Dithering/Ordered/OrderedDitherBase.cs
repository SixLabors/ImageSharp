// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Dithering.Base
{
    /// <summary>
    /// The base class for performing ordered ditheroing using a 4x4 matrix.
    /// </summary>
    public abstract class OrderedDitherBase : IOrderedDither
    {
        /// <summary>
        /// The dithering matrix
        /// </summary>
        private Fast2DArray<byte> matrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDitherBase"/> class.
        /// </summary>
        /// <param name="matrix">The thresholding matrix. </param>
        internal OrderedDitherBase(Fast2DArray<byte> matrix)
        {
            this.matrix = matrix;
        }

        /// <inheritdoc />
        public void Dither<TPixel>(ImageFrame<TPixel> image, TPixel source, TPixel upper, TPixel lower, byte[] bytes, int index, int x, int y)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: This doesn't really cut it for me.
            // I'd rather be using float but we need to add some sort of normalization vector methods to all IPixel implementations
            // before we can do that as the vectors all cover different ranges.
            source.ToXyzwBytes(bytes, 0);
            image[x, y] = this.matrix[y % 3, x % 3] >= bytes[index] ? lower : upper;
        }
    }
}
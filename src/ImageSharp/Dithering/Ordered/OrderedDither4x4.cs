// <copyright file="OrderedDither4x4.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering.Ordered
{
    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// The base class for performing ordered ditheroing using a 4x4 matrix.
    /// </summary>
    public abstract class OrderedDither4x4 : IOrderedDither
    {
        /// <summary>
        /// The dithering matrix
        /// </summary>
        private Fast2DArray<byte> matrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDither4x4"/> class.
        /// </summary>
        /// <param name="matrix">The thresholding matrix. </param>
        internal OrderedDither4x4(Fast2DArray<byte> matrix)
        {
            this.matrix = matrix;
        }

        /// <inheritdoc />
        public void Dither<TPixel>(ImageBase<TPixel> image, TPixel source, TPixel upper, TPixel lower, byte[] bytes, int index, int x, int y, int width, int height)
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
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Dithering.Base
{
    /// <summary>
    /// The base class for performing ordered dithering using a 4x4 matrix.
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
        public void Dither<TPixel>(ImageFrame<TPixel> image, TPixel source, TPixel upper, TPixel lower, ref Rgba32 rgba, int index, int x, int y)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ToRgba32(ref rgba);
            switch (index)
            {
                case 0:
                    image[x, y] = this.matrix[y % 3, x % 3] >= rgba.R ? lower : upper;
                    return;
                case 1:
                    image[x, y] = this.matrix[y % 3, x % 3] >= rgba.G ? lower : upper;
                    return;
                case 2:
                    image[x, y] = this.matrix[y % 3, x % 3] >= rgba.B ? lower : upper;
                    return;
                case 3:
                    image[x, y] = this.matrix[y % 3, x % 3] >= rgba.A ? lower : upper;
                    return;
            }

            throw new ArgumentOutOfRangeException(nameof(index), "Index should be between 0 and 3 inclusive.");
        }
    }
}
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Dithering
{
    /// <summary>
    /// The base class for performing ordered dithering using a dither matrix.
    /// </summary>
    public abstract class OrderedDitherBase : IOrderedDither
    {
        private readonly Fast2DArray<uint> matrix;
        private readonly Fast2DArray<uint> thresholdMatrix;
        private readonly int modulusX;
        private readonly int modulusY;

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderedDitherBase"/> class.
        /// </summary>
        /// <param name="matrix">The thresholding matrix. </param>
        internal OrderedDitherBase(Fast2DArray<uint> matrix)
        {
            this.matrix = matrix;
            this.modulusX = matrix.Width;
            this.modulusY = matrix.Height;
            this.thresholdMatrix = new Fast2DArray<uint>(matrix.Width, matrix.Height);

            // Adjust the matrix range for 0-255
            int multiplier = 256 / (this.modulusX * this.modulusY);
            for (int y = 0; y < matrix.Height; y++)
            {
                for (int x = 0; x < matrix.Width; x++)
                {
                    this.thresholdMatrix[y, x] = (uint)((matrix[y, x] + 1) * multiplier) - 1;
                }
            }
        }

        /// <inheritdoc />
        public void Dither<TPixel>(ImageFrame<TPixel> image, TPixel source, TPixel upper, TPixel lower, byte threshold, int x, int y)
            where TPixel : struct, IPixel<TPixel>
        {
            image[x, y] = this.thresholdMatrix[y % this.modulusY, x % this.modulusX] >= threshold ? lower : upper;
        }
    }
}
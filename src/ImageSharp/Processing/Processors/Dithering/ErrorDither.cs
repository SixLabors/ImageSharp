// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// The base class of all error diffusion dithering implementations.
    /// </summary>
    public abstract class ErrorDither : IDither
    {
        private readonly int offset;
        private readonly DenseMatrix<float> matrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDither"/> class.
        /// </summary>
        /// <param name="matrix">The diffusion matrix.</param>
        /// <param name="offset">The starting offset within the matrix.</param>
        protected ErrorDither(in DenseMatrix<float> matrix, int offset)
        {
            this.matrix = matrix;
            this.offset = offset;
        }

        /// <inheritdoc/>
        public DitherTransformColorBehavior TransformColorBehavior { get; } = DitherTransformColorBehavior.PreOperation;

        /// <inheritdoc/>
        public TPixel Dither<TPixel>(
            ImageFrame<TPixel> image,
            Rectangle bounds,
            TPixel source,
            TPixel transformed,
            int x,
            int y,
            int bitDepth)
            where TPixel : struct, IPixel<TPixel>
        {
            // Equal? Break out as there's no error to pass.
            if (source.Equals(transformed))
            {
                return transformed;
            }

            // Calculate the error
            Vector4 error = source.ToVector4() - transformed.ToVector4();

            int offset = this.offset;
            DenseMatrix<float> matrix = this.matrix;

            // Loop through and distribute the error amongst neighboring pixels.
            for (int row = 0, targetY = y; row < matrix.Rows; row++, targetY++)
            {
                if (targetY >= bounds.Bottom)
                {
                    continue;
                }

                Span<TPixel> rowSpan = image.GetPixelRowSpan(targetY);

                for (int col = 0; col < matrix.Columns; col++)
                {
                    int targetX = x + (col - offset);
                    if (targetX < bounds.Left || targetX >= bounds.Right)
                    {
                        continue;
                    }

                    float coefficient = matrix[row, col];
                    if (coefficient == 0)
                    {
                        continue;
                    }

                    ref TPixel pixel = ref rowSpan[targetX];
                    var result = pixel.ToVector4();

                    result += error * coefficient;
                    pixel.FromVector4(result);
                }
            }

            return transformed;
        }
    }
}

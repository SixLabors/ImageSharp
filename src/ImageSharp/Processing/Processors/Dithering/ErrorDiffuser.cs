// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Dithering
{
    /// <summary>
    /// The base class for performing error diffusion based dithering.
    /// </summary>
    public abstract class ErrorDiffuser : IErrorDiffuser
    {
        private readonly int offset;
        private readonly DenseMatrix<float> matrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDiffuser"/> class.
        /// </summary>
        /// <param name="matrix">The dithering matrix.</param>
        internal ErrorDiffuser(in DenseMatrix<float> matrix)
        {
            // Calculate the offset position of the pixel relative to
            // the diffusion matrix.
            this.offset = 0;

            for (int col = 0; col < matrix.Columns; col++)
            {
                if (matrix[0, col] != 0)
                {
                    this.offset = col - 1;
                    break;
                }
            }

            this.matrix = matrix;
        }

        /// <inheritdoc />
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Dither<TPixel>(ImageFrame<TPixel> image, TPixel source, TPixel transformed, int x, int y, int minX, int maxX, int maxY)
            where TPixel : struct, IPixel<TPixel>
        {
            image[x, y] = transformed;

            // Equal? Break out as there's no error to pass.
            if (source.Equals(transformed))
            {
                return;
            }

            // Calculate the error
            Vector4 error = source.ToVector4() - transformed.ToVector4();
            this.DoDither(image, x, y, minX, maxX, maxY, error);
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private void DoDither<TPixel>(ImageFrame<TPixel> image, int x, int y, int minX, int maxX, int maxY, Vector4 error)
            where TPixel : struct, IPixel<TPixel>
        {
            int offset = this.offset;
            DenseMatrix<float> matrix = this.matrix;

            // Loop through and distribute the error amongst neighboring pixels.
            for (int row = 0, targetY = y; row < matrix.Rows && targetY < maxY; row++, targetY++)
            {
                Span<TPixel> rowSpan = image.GetPixelRowSpan(targetY);

                for (int col = 0; col < matrix.Columns; col++)
                {
                    int targetX = x + (col - offset);
                    if (targetX >= minX && targetX < maxX)
                    {
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
            }
        }
    }
}

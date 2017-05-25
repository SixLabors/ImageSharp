// <copyright file="ErrorDiffuser.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Dithering
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// The base class for performing error diffusion based dithering.
    /// </summary>
    public abstract class ErrorDiffuser : IErrorDiffuser
    {
        /// <summary>
        /// The vector to perform division.
        /// </summary>
        private readonly Vector4 divisorVector;

        /// <summary>
        /// The matrix width
        /// </summary>
        private readonly int matrixHeight;

        /// <summary>
        /// The matrix height
        /// </summary>
        private readonly int matrixWidth;

        /// <summary>
        /// The offset at which to start the dithering operation.
        /// </summary>
        private readonly int startingOffset;

        /// <summary>
        /// The diffusion matrix
        /// </summary>
        private readonly Fast2DArray<float> matrix;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorDiffuser"/> class.
        /// </summary>
        /// <param name="matrix">The dithering matrix.</param>
        /// <param name="divisor">The divisor.</param>
        internal ErrorDiffuser(Fast2DArray<float> matrix, byte divisor)
        {
            Guard.NotNull(matrix, nameof(matrix));
            Guard.MustBeGreaterThan(divisor, 0, nameof(divisor));

            this.matrix = matrix;
            this.matrixWidth = this.matrix.Width;
            this.matrixHeight = this.matrix.Height;
            this.divisorVector = new Vector4(divisor);

            this.startingOffset = 0;
            for (int i = 0; i < this.matrixWidth; i++)
            {
                // Good to disable here as we are not comparing matematical output.
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (matrix[0, i] != 0)
                {
                    this.startingOffset = (byte)(i - 1);
                    break;
                }
            }
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dither<TPixel>(ImageBase<TPixel> pixels, TPixel source, TPixel transformed, int x, int y, int width, int height)
            where TPixel : struct, IPixel<TPixel>
        {
            this.Dither(pixels, source, transformed, x, y, width, height, true);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dither<TPixel>(ImageBase<TPixel> image, TPixel source, TPixel transformed, int x, int y, int width, int height, bool replacePixel)
            where TPixel : struct, IPixel<TPixel>
        {
            if (replacePixel)
            {
                // Assign the transformed pixel to the array.
                image[x, y] = transformed;
            }

            // Calculate the error
            Vector4 error = source.ToVector4() - transformed.ToVector4();

            // Loop through and distribute the error amongst neighbouring pixels.
            for (int row = 0; row < this.matrixHeight; row++)
            {
                int matrixY = y + row;
                if (matrixY > 0 && matrixY < height)
                {
                    Span<TPixel> rowSpan = image.GetRowSpan(matrixY);

                    for (int col = 0; col < this.matrixWidth; col++)
                    {
                        int matrixX = x + (col - this.startingOffset);

                        if (matrixX > 0 && matrixX < width)
                        {
                            float coefficient = this.matrix[row, col];

                            // Good to disable here as we are not comparing mathematical output.
                            // ReSharper disable once CompareOfFloatsByEqualityOperator
                            if (coefficient == 0)
                            {
                                continue;
                            }

                            ref TPixel pixel = ref rowSpan[matrixX];
                            var offsetColor = pixel.ToVector4();
                            var coefficientVector = new Vector4(coefficient);

                            Vector4 result = ((error * coefficientVector) / this.divisorVector) + offsetColor;
                            result.W = offsetColor.W;
                            pixel.PackFromVector4(result);
                        }
                    }
                }
            }
        }
    }
}
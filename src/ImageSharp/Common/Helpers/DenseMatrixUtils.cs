// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for <see cref="DenseMatrix{T}"/>.
    /// </summary>
    internal static class DenseMatrixUtils
    {
        /// <summary>
        /// Computes the sum of vectors in <paramref name="targetRow"/> weighted by the kernel weight values.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="matrix">The dense matrix.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRow">The target row.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        /// <param name="maxRow">The maximum working area row.</param>
        /// <param name="maxColumn">The maximum working area column.</param>
        /// <param name="offsetColumn">The column offset to apply to source sampling.</param>
        public static void Convolve<TPixel>(
            in DenseMatrix<float> matrix,
            Buffer2D<TPixel> sourcePixels,
            Span<Vector4> targetRow,
            int row,
            int column,
            int maxRow,
            int maxColumn,
            int offsetColumn)
            where TPixel : struct, IPixel<TPixel>
        {
            Vector4 vector = default;
            int matrixHeight = matrix.Rows;
            int matrixWidth = matrix.Columns;
            int radiusY = matrixHeight >> 1;
            int radiusX = matrixWidth >> 1;

            for (int y = 0; y < matrixHeight; y++)
            {
                int offsetY = (row + y - radiusY).Clamp(0, maxRow);
                Span<TPixel> sourceRowSpan = sourcePixels.GetRowSpan(offsetY);

                for (int x = 0; x < matrixWidth; x++)
                {
                    int offsetX = (column + offsetColumn + x - radiusX).Clamp(offsetColumn, maxColumn);
                    var currentColor = sourceRowSpan[offsetX].ToVector4();
                    Vector4Utils.Premultiply(ref currentColor);
                    currentColor *= matrix[y, x];
                    vector += currentColor;
                }
            }

            ref Vector4 target = ref targetRow[column];
            vector.W = target.W;
            Vector4Utils.UnPremultiply(ref vector);
            target = vector;
        }

        /// <summary>
        /// Computes the sum of vectors in <paramref name="targetRow"/> weighted by the two kernel weight values.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="matrixY">The vertical dense matrix.</param>
        /// <param name="matrixX">The horizontal dense matrix.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRow">The target row.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        /// <param name="maxRow">The maximum working area row.</param>
        /// <param name="maxColumn">The maximum working area column.</param>
        /// <param name="offsetColumn">The column offset to apply to source sampling.</param>
        public static void Convolve2D<TPixel>(
            in DenseMatrix<float> matrixY,
            in DenseMatrix<float> matrixX,
            Buffer2D<TPixel> sourcePixels,
            Span<Vector4> targetRow,
            int row,
            int column,
            int maxRow,
            int maxColumn,
            int offsetColumn)
            where TPixel : struct, IPixel<TPixel>
        {
            Vector4 vectorY = default;
            Vector4 vectorX = default;
            int matrixYHeight = matrixY.Rows;
            int matrixYWidth = matrixY.Columns;
            int matrixXHeight = matrixX.Rows;
            int matrixXWidth = matrixX.Columns;
            int radiusY = matrixYHeight >> 1;
            int radiusX = matrixXWidth >> 1;

            for (int y = 0; y < matrixYHeight; y++)
            {
                int offsetY = (row + y - radiusY).Clamp(0, maxRow);
                Span<TPixel> sourceRowSpan = sourcePixels.GetRowSpan(offsetY);

                for (int x = 0; x < matrixXWidth; x++)
                {
                    int offsetX = (column + offsetColumn + x - radiusX).Clamp(offsetColumn, maxColumn);
                    var currentColor = sourceRowSpan[offsetX].ToVector4();
                    Vector4Utils.Premultiply(ref currentColor);

                    if (y < matrixXHeight)
                    {
                        vectorX += matrixX[y, x] * currentColor;
                    }

                    if (x < matrixYWidth)
                    {
                        vectorY += matrixY[y, x] * currentColor;
                    }
                }
            }

            var vector = Vector4.SquareRoot((vectorX * vectorX) + (vectorY * vectorY));
            ref Vector4 target = ref targetRow[column];
            vector.W = target.W;
            Vector4Utils.UnPremultiply(ref vector);
            target = vector;
        }
    }
}
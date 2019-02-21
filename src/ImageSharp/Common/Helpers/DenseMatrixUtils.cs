﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for <see cref="DenseMatrix{T}"/>.
    /// TODO: One day rewrite all this to use SIMD intrinsics. There's a lot of scope for improvement.
    /// </summary>
    internal static class DenseMatrixUtils
    {
        /// <summary>
        /// Computes the sum of vectors in the span referenced by <paramref name="targetRowRef"/> weighted by the two kernel weight values.
        /// Using this method the convolution filter is not applied to alpha in addition to the color channels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="matrixY">The vertical dense matrix.</param>
        /// <param name="matrixX">The horizontal dense matrix.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        /// <param name="minRow">The minimum working area row.</param>
        /// <param name="maxRow">The maximum working area row.</param>
        /// <param name="minColumn">The minimum working area column.</param>
        /// <param name="maxColumn">The maximum working area column.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve2D3<TPixel>(
            in DenseMatrix<float> matrixY,
            in DenseMatrix<float> matrixX,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column,
            int minRow,
            int maxRow,
            int minColumn,
            int maxColumn)
            where TPixel : struct, IPixel<TPixel>
        {
            Vector4 vector = default;

            Convolve2DImpl(
                in matrixY,
                in matrixX,
                sourcePixels,
                row,
                column,
                minRow,
                maxRow,
                minColumn,
                maxColumn,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            vector.W = target.W;

            Vector4Utils.UnPremultiply(ref vector);
            target = vector;
        }

        /// <summary>
        /// Computes the sum of vectors in the span referenced by <paramref name="targetRowRef"/> weighted by the two kernel weight values.
        /// Using this method the convolution filter is applied to alpha in addition to the color channels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="matrixY">The vertical dense matrix.</param>
        /// <param name="matrixX">The horizontal dense matrix.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        /// <param name="minRow">The minimum working area row.</param>
        /// <param name="maxRow">The maximum working area row.</param>
        /// <param name="minColumn">The minimum working area column.</param>
        /// <param name="maxColumn">The maximum working area column.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve2D4<TPixel>(
            in DenseMatrix<float> matrixY,
            in DenseMatrix<float> matrixX,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column,
            int minRow,
            int maxRow,
            int minColumn,
            int maxColumn)
            where TPixel : struct, IPixel<TPixel>
        {
            Vector4 vector = default;

            Convolve2DImpl(
                in matrixY,
                in matrixX,
                sourcePixels,
                row,
                column,
                minRow,
                maxRow,
                minColumn,
                maxColumn,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            Vector4Utils.UnPremultiply(ref vector);
            target = vector;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve2DImpl<TPixel>(
            in DenseMatrix<float> matrixY,
            in DenseMatrix<float> matrixX,
            Buffer2D<TPixel> sourcePixels,
            int row,
            int column,
            int minRow,
            int maxRow,
            int minColumn,
            int maxColumn,
            ref Vector4 vector)
            where TPixel : struct, IPixel<TPixel>
        {
            Vector4 vectorY = default;
            Vector4 vectorX = default;
            int matrixHeight = matrixY.Rows;
            int matrixWidth = matrixY.Columns;
            int radiusY = matrixHeight >> 1;
            int radiusX = matrixWidth >> 1;
            int sourceOffsetColumnBase = column + minColumn;

            for (int y = 0; y < matrixHeight; y++)
            {
                int offsetY = (row + y - radiusY).Clamp(minRow, maxRow);
                Span<TPixel> sourceRowSpan = sourcePixels.GetRowSpan(offsetY);

                for (int x = 0; x < matrixWidth; x++)
                {
                    int offsetX = (sourceOffsetColumnBase + x - radiusX).Clamp(minColumn, maxColumn);
                    var currentColor = sourceRowSpan[offsetX].ToVector4();
                    Vector4Utils.Premultiply(ref currentColor);

                    vectorX += matrixX[y, x] * currentColor;
                    vectorY += matrixY[y, x] * currentColor;
                }
            }

            vector = Vector4.SquareRoot((vectorX * vectorX) + (vectorY * vectorY));
        }

        /// <summary>
        /// Computes the sum of vectors in the span referenced by <paramref name="targetRowRef"/> weighted by the kernel weight values.
        /// Using this method the convolution filter is not applied to alpha in addition to the color channels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="matrix">The dense matrix.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        /// <param name="minRow">The minimum working area row.</param>
        /// <param name="maxRow">The maximum working area row.</param>
        /// <param name="minColumn">The minimum working area column.</param>
        /// <param name="maxColumn">The maximum working area column.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve3<TPixel>(
            in DenseMatrix<float> matrix,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column,
            int minRow,
            int maxRow,
            int minColumn,
            int maxColumn)
            where TPixel : struct, IPixel<TPixel>
        {
            Vector4 vector = default;

            ConvolveImpl(
                in matrix,
                sourcePixels,
                row,
                column,
                minRow,
                maxRow,
                minColumn,
                maxColumn,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            vector.W = target.W;

            Vector4Utils.UnPremultiply(ref vector);
            target = vector;
        }

        /// <summary>
        /// Computes the sum of vectors in the span referenced by <paramref name="targetRowRef"/> weighted by the kernel weight values.
        /// Using this method the convolution filter is applied to alpha in addition to the color channels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="matrix">The dense matrix.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        /// <param name="minRow">The minimum working area row.</param>
        /// <param name="maxRow">The maximum working area row.</param>
        /// <param name="minColumn">The minimum working area column.</param>
        /// <param name="maxColumn">The maximum working area column.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve4<TPixel>(
            in DenseMatrix<float> matrix,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column,
            int minRow,
            int maxRow,
            int minColumn,
            int maxColumn)
            where TPixel : struct, IPixel<TPixel>
        {
            Vector4 vector = default;

            ConvolveImpl(
                in matrix,
                sourcePixels,
                row,
                column,
                minRow,
                maxRow,
                minColumn,
                maxColumn,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            Vector4Utils.UnPremultiply(ref vector);
            target = vector;
        }

        /// <summary>
        /// Computes the sum of vectors in <paramref name="targetRow"/> weighted by the kernel weight values.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="kernel">The 1D convolution kernel.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRow">The target row.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        /// <param name="minRow">The minimum working area row.</param>
        /// <param name="maxRow">The maximum working area row.</param>
        /// <param name="minColumn">The minimum working area column.</param>
        /// <param name="maxColumn">The maximum working area column.</param>
        public static void Convolve1D<TPixel>(
            Span<Complex64> kernel,
            Buffer2D<TPixel> sourcePixels,
            Span<ComplexVector4> targetRow,
            int row,
            int column,
            int minRow,
            int maxRow,
            int minColumn,
            int maxColumn)
            where TPixel : struct, IPixel<TPixel>
        {
            ComplexVector4 vector = default;
            int kernelLength = kernel.Length;
            int radiusY = kernelLength >> 1;
            int sourceOffsetColumnBase = column + minColumn;
            ref Complex64 baseRef = ref MemoryMarshal.GetReference(kernel);

            for (int i = 0; i < kernelLength; i++)
            {
                int offsetY = (row + i - radiusY).Clamp(minRow, maxRow);
                int offsetX = sourceOffsetColumnBase.Clamp(minColumn, maxColumn);
                Span<TPixel> sourceRowSpan = sourcePixels.GetRowSpan(offsetY);
                var currentColor = sourceRowSpan[offsetX].ToVector4();

                vector.Sum(Unsafe.Add(ref baseRef, i) * currentColor);
            }

            targetRow[column] = vector;
        }

        /// <summary>
        /// Computes the sum of vectors in <paramref name="targetRow"/> weighted by the kernel weight values.
        /// </summary>
        /// <param name="kernel">The 1D convolution kernel.</param>
        /// <param name="sourceValues">The source frame.</param>
        /// <param name="targetRow">The target row.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        /// <param name="minRow">The minimum working area row.</param>
        /// <param name="maxRow">The maximum working area row.</param>
        /// <param name="minColumn">The minimum working area column.</param>
        /// <param name="maxColumn">The maximum working area column.</param>
        public static void Convolve1D(
            Span<Complex64> kernel,
            Buffer2D<ComplexVector4> sourceValues,
            Span<ComplexVector4> targetRow,
            int row,
            int column,
            int minRow,
            int maxRow,
            int minColumn,
            int maxColumn)
        {
            ComplexVector4 vector = default;
            int kernelLength = kernel.Length;
            int radiusX = kernelLength >> 1;
            int sourceOffsetColumnBase = column + minColumn;

            int offsetY = row.Clamp(minRow, maxRow);
            ref ComplexVector4 sourceRef = ref MemoryMarshal.GetReference(sourceValues.GetRowSpan(offsetY));
            ref Complex64 baseRef = ref MemoryMarshal.GetReference(kernel);

            for (int x = 0; x < kernelLength; x++)
            {
                int offsetX = (sourceOffsetColumnBase + x - radiusX).Clamp(minColumn, maxColumn);
                vector.Sum(Unsafe.Add(ref baseRef, x) * Unsafe.Add(ref sourceRef, offsetX));
            }

            targetRow[column] = vector;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ConvolveImpl<TPixel>(
            in DenseMatrix<float> matrix,
            Buffer2D<TPixel> sourcePixels,
            int row,
            int column,
            int minRow,
            int maxRow,
            int minColumn,
            int maxColumn,
            ref Vector4 vector)
            where TPixel : struct, IPixel<TPixel>
        {
            int matrixHeight = matrix.Rows;
            int matrixWidth = matrix.Columns;
            int radiusY = matrixHeight >> 1;
            int radiusX = matrixWidth >> 1;
            int sourceOffsetColumnBase = column + minColumn;

            for (int y = 0; y < matrixHeight; y++)
            {
                int offsetY = (row + y - radiusY).Clamp(minRow, maxRow);
                Span<TPixel> sourceRowSpan = sourcePixels.GetRowSpan(offsetY);

                for (int x = 0; x < matrixWidth; x++)
                {
                    int offsetX = (sourceOffsetColumnBase + x - radiusX).Clamp(minColumn, maxColumn);
                    var currentColor = sourceRowSpan[offsetX].ToVector4();
                    Vector4Utils.Premultiply(ref currentColor);
                    vector += matrix[y, x] * currentColor;
                }
            }
        }
    }
}

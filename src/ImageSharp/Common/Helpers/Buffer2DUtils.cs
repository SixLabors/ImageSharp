// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for <see cref="Buffer2D{T}"/>.
    /// TODO: One day rewrite all this to use SIMD intrinsics. There's a lot of scope for improvement.
    /// </summary>
    internal static class Buffer2DUtils
    {
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
        public static void Convolve4<TPixel>(
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
        public static void Convolve4(
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
    }
}

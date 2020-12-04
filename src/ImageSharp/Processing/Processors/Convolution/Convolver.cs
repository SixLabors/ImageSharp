// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Provides methods to perform convolution operations.
    /// </summary>
    internal static class Convolver
    {
        /// <summary>
        /// Computes the sum of vectors in the span referenced by <paramref name="targetRowRef"/> weighted by the two kernel weight values.
        /// Using this method the convolution filter is not applied to alpha in addition to the color channels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="kernelY">The vertical convolution kernel.</param>
        /// <param name="kernelX">The horizontal convolution kernel.</param>
        /// <param name="rowSampleOffsets">The span containing precalculated kernel y-sampling offsets.</param>
        /// <param name="columnSampleOffsets">The span containing precalculated kernel x-sampling offsets.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve2D3<TPixel>(
            in DenseMatrix<float> kernelY,
            in DenseMatrix<float> kernelX,
            Span<int> rowSampleOffsets,
            Span<int> columnSampleOffsets,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vector = default;

            Convolve2DImpl(
                in kernelY,
                in kernelX,
                rowSampleOffsets,
                columnSampleOffsets,
                sourcePixels,
                row,
                column,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            vector.W = target.W;

            Numerics.UnPremultiply(ref vector);
            target = vector;
        }

        /// <summary>
        /// Computes the sum of vectors in the span referenced by <paramref name="targetRowRef"/> weighted by the two kernel weight values.
        /// Using this method the convolution filter is applied to alpha in addition to the color channels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="kernelY">The vertical convolution kernel.</param>
        /// <param name="kernelX">The horizontal convolution kernel.</param>
        /// <param name="rowSampleOffsets">The span containing precalculated kernel y-sampling offsets.</param>
        /// <param name="columnSampleOffsets">The span containing precalculated kernel x-sampling offsets.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve2D4<TPixel>(
            in DenseMatrix<float> kernelY,
            in DenseMatrix<float> kernelX,
            Span<int> rowSampleOffsets,
            Span<int> columnSampleOffsets,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vector = default;

            Convolve2DImpl(
                in kernelY,
                in kernelX,
                rowSampleOffsets,
                columnSampleOffsets,
                sourcePixels,
                row,
                column,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            Numerics.UnPremultiply(ref vector);
            target = vector;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve2DImpl<TPixel>(
            in DenseMatrix<float> kernelY,
            in DenseMatrix<float> kernelX,
            Span<int> rowSampleOffsets,
            Span<int> columnSampleOffsets,
            Buffer2D<TPixel> sourcePixels,
            int row,
            int column,
            ref Vector4 targetVector)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vectorY = default;
            Vector4 vectorX = default;
            int kernelHeight = kernelY.Rows;
            int kernelWidth = kernelY.Columns;

            for (int y = 0; y < kernelHeight; y++)
            {
                int offsetY = rowSampleOffsets[(row * kernelHeight) + y];
                Span<TPixel> sourceRowSpan = sourcePixels.GetRowSpan(offsetY);

                for (int x = 0; x < kernelWidth; x++)
                {
                    int offsetX = columnSampleOffsets[(column * kernelWidth) + x];
                    var sample = sourceRowSpan[offsetX].ToVector4();
                    Numerics.Premultiply(ref sample);

                    vectorX += kernelX[y, x] * sample;
                    vectorY += kernelY[y, x] * sample;
                }
            }

            targetVector = Vector4.SquareRoot((vectorX * vectorX) + (vectorY * vectorY));
        }

        /// <summary>
        /// Computes the sum of vectors in the span referenced by <paramref name="targetRowRef"/> weighted by the kernel weight values.
        /// Using this method the convolution filter is not applied to alpha in addition to the color channels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="kernel">The convolution kernel.</param>
        /// <param name="rowSampleOffsets">The span containing precalculated kernel y-sampling offsets.</param>
        /// <param name="columnSampleOffsets">The span containing precalculated kernel x-sampling offsets.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve3<TPixel>(
            in DenseMatrix<float> kernel,
            Span<int> rowSampleOffsets,
            Span<int> columnSampleOffsets,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vector = default;

            ConvolveImpl(
                in kernel,
                rowSampleOffsets,
                columnSampleOffsets,
                sourcePixels,
                row,
                column,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            vector.W = target.W;

            Numerics.UnPremultiply(ref vector);
            target = vector;
        }

        /// <summary>
        /// Computes the sum of vectors in the span referenced by <paramref name="targetRowRef"/> weighted by the kernel weight values.
        /// Using this method the convolution filter is applied to alpha in addition to the color channels.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="kernel">The convolution kernel.</param>
        /// <param name="rowSampleOffsets">The span containing precalculated kernel y-offsets.</param>
        /// <param name="columnSampleOffsets">The span containing precalculated kernel x-offsets.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static void Convolve4<TPixel>(
            in DenseMatrix<float> kernel,
            Span<int> rowSampleOffsets,
            Span<int> columnSampleOffsets,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vector = default;

            ConvolveImpl(
                in kernel,
                rowSampleOffsets,
                columnSampleOffsets,
                sourcePixels,
                row,
                column,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            Numerics.UnPremultiply(ref vector);
            target = vector;
        }

        [MethodImpl(InliningOptions.ShortMethod)]
        private static void ConvolveImpl<TPixel>(
            in DenseMatrix<float> kernel,
            Span<int> rowSampleOffsets,
            Span<int> columnSampleOffsets,
            Buffer2D<TPixel> sourcePixels,
            int row,
            int column,
            ref Vector4 targetVector)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            int kernelHeight = kernel.Rows;
            int kernelWidth = kernel.Columns;

            for (int y = 0; y < kernelHeight; y++)
            {
                int offsetY = rowSampleOffsets[(row * kernelHeight) + y];
                Span<TPixel> sourceRowSpan = sourcePixels.GetRowSpan(offsetY);

                for (int x = 0; x < kernelWidth; x++)
                {
                    int offsetX = columnSampleOffsets[(column * kernelWidth) + x];
                    var sample = sourceRowSpan[offsetX].ToVector4();
                    Numerics.Premultiply(ref sample);
                    targetVector += kernel[y, x] * sample;
                }
            }
        }
    }
}

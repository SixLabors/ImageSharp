// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

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
        /// <param name="state">The 2D convolution kernels state.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convolve2D3<TPixel>(
            in Convolution2DState state,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vector = default;

            Convolve2DImpl(
                in state,
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
        /// <param name="state">The 2D convolution kernels state.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convolve2D4<TPixel>(
            in Convolution2DState state,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vector = default;

            Convolve2DImpl(
                in state,
                sourcePixels,
                row,
                column,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            Numerics.UnPremultiply(ref vector);
            target = vector;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convolve2DImpl<TPixel>(
            in Convolution2DState state,
            Buffer2D<TPixel> sourcePixels,
            int row,
            int column,
            ref Vector4 targetVector)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ReadOnlyKernel kernelY = state.KernelY;
            ReadOnlyKernel kernelX = state.KernelX;
            int kernelHeight = kernelY.Rows;
            int kernelWidth = kernelY.Columns;

            Vector4 vectorY = default;
            Vector4 vectorX = default;

            for (int y = 0; y < kernelHeight; y++)
            {
                int offsetY = state.GetRowSampleOffset(row, y);
                ref TPixel sourceRowBase = ref MemoryMarshal.GetReference(sourcePixels.GetRowSpan(offsetY));

                for (int x = 0; x < kernelWidth; x++)
                {
                    int offsetX = state.GetColumnSampleOffset(column, x);
                    var sample = Unsafe.Add(ref sourceRowBase, offsetX).ToVector4();
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
        /// <param name="state">The convolution kernel state.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convolve3<TPixel>(
            in ConvolutionState state,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vector = default;

            ConvolveImpl(
                state,
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
        /// <param name="state">The convolution kernel state.</param>
        /// <param name="sourcePixels">The source frame.</param>
        /// <param name="targetRowRef">The target row base reference.</param>
        /// <param name="row">The current row.</param>
        /// <param name="column">The current column.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Convolve4<TPixel>(
            in ConvolutionState state,
            Buffer2D<TPixel> sourcePixels,
            ref Vector4 targetRowRef,
            int row,
            int column)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            Vector4 vector = default;

            ConvolveImpl(
                state,
                sourcePixels,
                row,
                column,
                ref vector);

            ref Vector4 target = ref Unsafe.Add(ref targetRowRef, column);
            Numerics.UnPremultiply(ref vector);
            target = vector;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ConvolveImpl<TPixel>(
            in ConvolutionState state,
            Buffer2D<TPixel> sourcePixels,
            int row,
            int column,
            ref Vector4 targetVector)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            ReadOnlyKernel kernel = state.Kernel;
            int kernelHeight = kernel.Rows;
            int kernelWidth = kernel.Columns;

            for (int y = 0; y < kernelHeight; y++)
            {
                int offsetY = state.GetRowSampleOffset(row, y);
                ref TPixel sourceRowBase = ref MemoryMarshal.GetReference(sourcePixels.GetRowSpan(offsetY));

                for (int x = 0; x < kernelWidth; x++)
                {
                    int offsetX = state.GetColumnSampleOffset(column, x);
                    var sample = Unsafe.Add(ref sourceRowBase, offsetX).ToVector4();
                    Numerics.Premultiply(ref sample);
                    targetVector += kernel[y, x] * sample;
                }
            }
        }
    }
}

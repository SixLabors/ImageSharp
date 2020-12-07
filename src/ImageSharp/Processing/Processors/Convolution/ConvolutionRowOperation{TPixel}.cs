// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// A <see langword="struct"/> implementing the logic for 1D convolution.
    /// </summary>
    internal readonly struct ConvolutionRowOperation<TPixel> : IRowOperation<Vector4>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Rectangle bounds;
        private readonly Buffer2D<TPixel> targetPixels;
        private readonly Buffer2D<TPixel> sourcePixels;
        private readonly KernelSamplingMap map;
        private readonly DenseMatrix<float> kernelMatrix;
        private readonly Configuration configuration;
        private readonly bool preserveAlpha;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ConvolutionRowOperation(
            Rectangle bounds,
            Buffer2D<TPixel> targetPixels,
            Buffer2D<TPixel> sourcePixels,
            KernelSamplingMap map,
            DenseMatrix<float> kernelMatrix,
            Configuration configuration,
            bool preserveAlpha)
        {
            this.bounds = bounds;
            this.targetPixels = targetPixels;
            this.sourcePixels = sourcePixels;
            this.map = map;
            this.kernelMatrix = kernelMatrix;
            this.configuration = configuration;
            this.preserveAlpha = preserveAlpha;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke(int y, Span<Vector4> span)
        {
            if (this.preserveAlpha)
            {
                this.Convolve3(y, span);
            }
            else
            {
                this.Convolve4(y, span);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Convolve3(int y, Span<Vector4> span)
        {
            // Span is 2x bounds.
            int boundsX = this.bounds.X;
            int boundsWidth = this.bounds.Width;
            Span<Vector4> sourceBuffer = span.Slice(0, this.bounds.Width);
            Span<Vector4> targetBuffer = span.Slice(this.bounds.Width);

            var state = new ConvolutionState(in this.kernelMatrix, this.map);
            ref int sampleRowBase = ref state.GetSampleRow(y - this.bounds.Y);

            // Clear the target buffer for each row run.
            targetBuffer.Clear();
            ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);

            ReadOnlyKernel kernel = state.Kernel;
            Span<TPixel> sourceRow;
            for (int kY = 0; kY < kernel.Rows; kY++)
            {
                // Get the precalculated source sample row for this kernel row and copy to our buffer.
                int sampleY = Unsafe.Add(ref sampleRowBase, kY);
                sourceRow = this.sourcePixels.GetRowSpan(sampleY).Slice(boundsX, boundsWidth);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);

                for (int x = 0; x < sourceBuffer.Length; x++)
                {
                    ref int sampleColumnBase = ref state.GetSampleColumn(x);
                    ref Vector4 target = ref Unsafe.Add(ref targetBase, x);

                    for (int kX = 0; kX < kernel.Columns; kX++)
                    {
                        int sampleX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                        Vector4 sample = Unsafe.Add(ref sourceBase, sampleX);
                        target += kernel[kY, kX] * sample;
                    }
                }
            }

            // Now we need to copy the original alpha values from the source row.
            sourceRow = this.sourcePixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
            PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

            for (int x = 0; x < sourceRow.Length; x++)
            {
                ref Vector4 target = ref Unsafe.Add(ref targetBase, x);
                target.W = Unsafe.Add(ref MemoryMarshal.GetReference(sourceBuffer), x).W;
            }

            Span<TPixel> targetRow = this.targetPixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetBuffer, targetRow);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Convolve4(int y, Span<Vector4> span)
        {
            // Span is 2x bounds.
            int boundsX = this.bounds.X;
            int boundsWidth = this.bounds.Width;
            Span<Vector4> sourceBuffer = span.Slice(0, this.bounds.Width);
            Span<Vector4> targetBuffer = span.Slice(this.bounds.Width);

            var state = new ConvolutionState(in this.kernelMatrix, this.map);
            ref int sampleRowBase = ref state.GetSampleRow(y - this.bounds.Y);

            // Clear the target buffer for each row run.
            targetBuffer.Clear();
            ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);

            ReadOnlyKernel kernel = state.Kernel;
            for (int kY = 0; kY < kernel.Rows; kY++)
            {
                // Get the precalculated source sample row for this kernel row and copy to our buffer.
                int sampleY = Unsafe.Add(ref sampleRowBase, kY);
                Span<TPixel> sourceRow = this.sourcePixels.GetRowSpan(sampleY).Slice(boundsX, boundsWidth);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                Numerics.Premultiply(sourceBuffer);
                ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);

                for (int x = 0; x < sourceBuffer.Length; x++)
                {
                    ref int sampleColumnBase = ref state.GetSampleColumn(x);
                    ref Vector4 target = ref Unsafe.Add(ref targetBase, x);

                    for (int kX = 0; kX < kernel.Columns; kX++)
                    {
                        int sampleX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                        Vector4 sample = Unsafe.Add(ref sourceBase, sampleX);
                        target += kernel[kY, kX] * sample;
                    }
                }
            }

            Numerics.UnPremultiply(targetBuffer);

            Span<TPixel> targetRow = this.targetPixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetBuffer, targetRow);
        }
    }
}

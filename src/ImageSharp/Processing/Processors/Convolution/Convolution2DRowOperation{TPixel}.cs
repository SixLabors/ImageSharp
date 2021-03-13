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
    /// A <see langword="struct"/> implementing the logic for 2D convolution.
    /// </summary>
    internal readonly struct Convolution2DRowOperation<TPixel> : IRowOperation<Vector4>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly Rectangle bounds;
        private readonly Buffer2D<TPixel> targetPixels;
        private readonly Buffer2D<TPixel> sourcePixels;
        private readonly KernelSamplingMap map;
        private readonly DenseMatrix<float> kernelMatrixY;
        private readonly DenseMatrix<float> kernelMatrixX;
        private readonly Configuration configuration;
        private readonly bool preserveAlpha;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Convolution2DRowOperation(
            Rectangle bounds,
            Buffer2D<TPixel> targetPixels,
            Buffer2D<TPixel> sourcePixels,
            KernelSamplingMap map,
            DenseMatrix<float> kernelMatrixY,
            DenseMatrix<float> kernelMatrixX,
            Configuration configuration,
            bool preserveAlpha)
        {
            this.bounds = bounds;
            this.targetPixels = targetPixels;
            this.sourcePixels = sourcePixels;
            this.map = map;
            this.kernelMatrixY = kernelMatrixY;
            this.kernelMatrixX = kernelMatrixX;
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
            // Span is 3x bounds.
            int boundsX = this.bounds.X;
            int boundsWidth = this.bounds.Width;
            Span<Vector4> sourceBuffer = span.Slice(0, boundsWidth);
            Span<Vector4> targetYBuffer = span.Slice(boundsWidth, boundsWidth);
            Span<Vector4> targetXBuffer = span.Slice(boundsWidth * 2, boundsWidth);

            var state = new Convolution2DState(in this.kernelMatrixY, in this.kernelMatrixX, this.map);
            ref int sampleRowBase = ref state.GetSampleRow(y - this.bounds.Y);

            // Clear the target buffers for each row run.
            targetYBuffer.Clear();
            targetXBuffer.Clear();
            ref Vector4 targetBaseY = ref MemoryMarshal.GetReference(targetYBuffer);
            ref Vector4 targetBaseX = ref MemoryMarshal.GetReference(targetXBuffer);

            ReadOnlyKernel kernelY = state.KernelY;
            ReadOnlyKernel kernelX = state.KernelX;
            Span<TPixel> sourceRow;
            for (int kY = 0; kY < kernelY.Rows; kY++)
            {
                // Get the precalculated source sample row for this kernel row and copy to our buffer.
                int sampleY = Unsafe.Add(ref sampleRowBase, kY);
                sourceRow = this.sourcePixels.GetRowSpan(sampleY).Slice(boundsX, boundsWidth);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);

                for (int x = 0; x < sourceBuffer.Length; x++)
                {
                    ref int sampleColumnBase = ref state.GetSampleColumn(x);
                    ref Vector4 targetY = ref Unsafe.Add(ref targetBaseY, x);
                    ref Vector4 targetX = ref Unsafe.Add(ref targetBaseX, x);

                    for (int kX = 0; kX < kernelY.Columns; kX++)
                    {
                        int sampleX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                        Vector4 sample = Unsafe.Add(ref sourceBase, sampleX);
                        targetY += kernelX[kY, kX] * sample;
                        targetX += kernelY[kY, kX] * sample;
                    }
                }
            }

            // Now we need to combine the values and copy the original alpha values
            // from the source row.
            sourceRow = this.sourcePixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
            PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

            for (int x = 0; x < sourceRow.Length; x++)
            {
                ref Vector4 target = ref Unsafe.Add(ref targetBaseY, x);
                Vector4 vectorY = target;
                Vector4 vectorX = Unsafe.Add(ref targetBaseX, x);

                target = Vector4.SquareRoot((vectorX * vectorX) + (vectorY * vectorY));
                target.W = Unsafe.Add(ref MemoryMarshal.GetReference(sourceBuffer), x).W;
            }

            Span<TPixel> targetRowSpan = this.targetPixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetYBuffer, targetRowSpan);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Convolve4(int y, Span<Vector4> span)
        {
            // Span is 3x bounds.
            int boundsX = this.bounds.X;
            int boundsWidth = this.bounds.Width;
            Span<Vector4> sourceBuffer = span.Slice(0, boundsWidth);
            Span<Vector4> targetYBuffer = span.Slice(boundsWidth, boundsWidth);
            Span<Vector4> targetXBuffer = span.Slice(boundsWidth * 2, boundsWidth);

            var state = new Convolution2DState(in this.kernelMatrixY, in this.kernelMatrixX, this.map);
            ref int sampleRowBase = ref state.GetSampleRow(y - this.bounds.Y);

            // Clear the target buffers for each row run.
            targetYBuffer.Clear();
            targetXBuffer.Clear();
            ref Vector4 targetBaseY = ref MemoryMarshal.GetReference(targetYBuffer);
            ref Vector4 targetBaseX = ref MemoryMarshal.GetReference(targetXBuffer);

            ReadOnlyKernel kernelY = state.KernelY;
            ReadOnlyKernel kernelX = state.KernelX;
            for (int kY = 0; kY < kernelY.Rows; kY++)
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
                    ref Vector4 targetY = ref Unsafe.Add(ref targetBaseY, x);
                    ref Vector4 targetX = ref Unsafe.Add(ref targetBaseX, x);

                    for (int kX = 0; kX < kernelY.Columns; kX++)
                    {
                        int sampleX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                        Vector4 sample = Unsafe.Add(ref sourceBase, sampleX);
                        targetY += kernelX[kY, kX] * sample;
                        targetX += kernelY[kY, kX] * sample;
                    }
                }
            }

            // Now we need to combine the values
            for (int x = 0; x < targetYBuffer.Length; x++)
            {
                ref Vector4 target = ref Unsafe.Add(ref targetBaseY, x);
                Vector4 vectorY = target;
                Vector4 vectorX = Unsafe.Add(ref targetBaseX, x);

                target = Vector4.SquareRoot((vectorX * vectorX) + (vectorY * vectorY));
            }

            Numerics.UnPremultiply(targetYBuffer);

            Span<TPixel> targetRow = this.targetPixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetYBuffer, targetRow);
        }
    }
}

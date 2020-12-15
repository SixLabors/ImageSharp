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
    /// Defines a processor that uses two one-dimensional matrices to perform two-pass convolution against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class Convolution2PassProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution2PassProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        /// <param name="preserveAlpha">Whether the convolution filter is applied to alpha as well as the color channels.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public Convolution2PassProcessor(
            Configuration configuration,
            in DenseMatrix<float> kernelX,
            in DenseMatrix<float> kernelY,
            bool preserveAlpha,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.KernelX = kernelX;
            this.KernelY = kernelY;
            this.PreserveAlpha = preserveAlpha;
        }

        /// <summary>
        /// Gets the horizontal convolution kernel.
        /// </summary>
        public DenseMatrix<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical convolution kernel.
        /// </summary>
        public DenseMatrix<float> KernelY { get; }

        /// <summary>
        /// Gets a value indicating whether the convolution filter is applied to alpha as well as the color channels.
        /// </summary>
        public bool PreserveAlpha { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            using Buffer2D<TPixel> firstPassPixels = this.Configuration.MemoryAllocator.Allocate2D<TPixel>(source.Size());

            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            // We use a rectangle 2x the interest width to allocate a buffer big enough
            // for source and target bulk pixel conversion.
            var operationBounds = new Rectangle(interest.X, interest.Y, interest.Width * 2, interest.Height);

            // We can create a single sampling map with the size as if we were using the non separated 2D kernel
            // the two 1D kernels represent, and reuse it across both convolution steps, like in the bokeh blur.
            using var mapXY = new KernelSamplingMap(this.Configuration.MemoryAllocator);

            mapXY.BuildSamplingOffsetMap(this.KernelY.Rows, this.KernelX.Columns, interest);

            // Horizontal convolution
            var horizontalOperation = new HorizontalConvolutionRowOperation(
                interest,
                firstPassPixels,
                source.PixelBuffer,
                mapXY,
                this.KernelX,
                this.Configuration,
                this.PreserveAlpha);

            ParallelRowIterator.IterateRows<HorizontalConvolutionRowOperation, Vector4>(
                this.Configuration,
                operationBounds,
                in horizontalOperation);

            // Vertical convolution
            var verticalOperation = new VerticalConvolutionRowOperation(
                interest,
                source.PixelBuffer,
                firstPassPixels,
                mapXY,
                this.KernelY,
                this.Configuration,
                this.PreserveAlpha);

            ParallelRowIterator.IterateRows<VerticalConvolutionRowOperation, Vector4>(
                this.Configuration,
                operationBounds,
                in verticalOperation);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the logic for the horizontal 1D convolution.
        /// </summary>
        internal readonly struct HorizontalConvolutionRowOperation : IRowOperation<Vector4>
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Buffer2D<TPixel> sourcePixels;
            private readonly KernelSamplingMap map;
            private readonly DenseMatrix<float> kernelMatrix;
            private readonly Configuration configuration;
            private readonly bool preserveAlpha;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public HorizontalConvolutionRowOperation(
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
                int kernelSize = this.kernelMatrix.Columns;

                Span<Vector4> sourceBuffer = span.Slice(0, this.bounds.Width);
                Span<Vector4> targetBuffer = span.Slice(this.bounds.Width);

                // Clear the target buffer for each row run.
                targetBuffer.Clear();
                ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);

                // Get the precalculated source sample row for this kernel row and copy to our buffer.
                Span<TPixel> sourceRow = this.sourcePixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);
                ref float kernelBase = ref this.kernelMatrix[0, 0];
                ref int sampleColumnBase = ref MemoryMarshal.GetReference(this.map.GetColumnOffsetSpan());

                for (int x = 0; x < sourceBuffer.Length; x++)
                {
                    ref Vector4 target = ref Unsafe.Add(ref targetBase, x);

                    for (int kX = 0; kX < kernelSize; kX++)
                    {
                        int sampleX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                        Vector4 sample = Unsafe.Add(ref sourceBase, sampleX);
                        float factor = Unsafe.Add(ref kernelBase, kX);

                        target += factor * sample;
                    }

                    // Shift the base column sampling reference by one row
                    sampleColumnBase = ref Unsafe.Add(ref sampleColumnBase, kernelSize);
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
                int kernelSize = this.kernelMatrix.Columns;

                Span<Vector4> sourceBuffer = span.Slice(0, this.bounds.Width);
                Span<Vector4> targetBuffer = span.Slice(this.bounds.Width);

                // Clear the target buffer for each row run.
                targetBuffer.Clear();
                ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);

                // Get the precalculated source sample row for this kernel row and copy to our buffer.
                Span<TPixel> sourceRow = this.sourcePixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                Numerics.Premultiply(sourceBuffer);

                ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);
                ref float kernelBase = ref this.kernelMatrix[0, 0];
                ref int sampleColumnBase = ref MemoryMarshal.GetReference(this.map.GetColumnOffsetSpan());

                for (int x = 0; x < sourceBuffer.Length; x++)
                {
                    ref Vector4 target = ref Unsafe.Add(ref targetBase, x);

                    for (int kX = 0; kX < kernelSize; kX++)
                    {
                        int sampleX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                        Vector4 sample = Unsafe.Add(ref sourceBase, sampleX);
                        float factor = Unsafe.Add(ref kernelBase, kX);

                        target += factor * sample;
                    }

                    sampleColumnBase = ref Unsafe.Add(ref sampleColumnBase, kernelSize);
                }

                Numerics.UnPremultiply(targetBuffer);

                Span<TPixel> targetRow = this.targetPixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetBuffer, targetRow);
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the logic for the vertical 1D convolution.
        /// </summary>
        internal readonly struct VerticalConvolutionRowOperation : IRowOperation<Vector4>
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Buffer2D<TPixel> sourcePixels;
            private readonly KernelSamplingMap map;
            private readonly DenseMatrix<float> kernelMatrix;
            private readonly Configuration configuration;
            private readonly bool preserveAlpha;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public VerticalConvolutionRowOperation(
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
                int kernelSize = this.kernelMatrix.Rows;

                Span<Vector4> sourceBuffer = span.Slice(0, this.bounds.Width);
                Span<Vector4> targetBuffer = span.Slice(this.bounds.Width);

                ref int sampleRowBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(this.map.GetRowOffsetSpan()), (y - this.bounds.Y) * kernelSize);

                // Clear the target buffer for each row run.
                targetBuffer.Clear();

                ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);
                ref float kernelBase = ref this.kernelMatrix[0, 0];

                Span<TPixel> sourceRow;
                for (int kY = 0; kY < kernelSize; kY++)
                {
                    // Get the precalculated source sample row for this kernel row and copy to our buffer.
                    int sampleY = Unsafe.Add(ref sampleRowBase, kY);
                    sourceRow = this.sourcePixels.GetRowSpan(sampleY).Slice(boundsX, boundsWidth);

                    PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                    ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);
                    float factor = Unsafe.Add(ref kernelBase, kY);

                    for (int x = 0; x < sourceBuffer.Length; x++)
                    {
                        ref Vector4 target = ref Unsafe.Add(ref targetBase, x);
                        Vector4 sample = Unsafe.Add(ref sourceBase, x);

                        target += factor * sample;
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
                int kernelSize = this.kernelMatrix.Rows;

                Span<Vector4> sourceBuffer = span.Slice(0, this.bounds.Width);
                Span<Vector4> targetBuffer = span.Slice(this.bounds.Width);

                var state = new ConvolutionState(in this.kernelMatrix, this.map);
                ref int sampleRowBase = ref state.GetSampleRow(y - this.bounds.Y);

                // Clear the target buffer for each row run.
                targetBuffer.Clear();

                ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);
                ref float kernelBase = ref this.kernelMatrix[0, 0];

                for (int kY = 0; kY < kernelSize; kY++)
                {
                    // Get the precalculated source sample row for this kernel row and copy to our buffer.
                    int sampleY = Unsafe.Add(ref sampleRowBase, kY);
                    Span<TPixel> sourceRow = this.sourcePixels.GetRowSpan(sampleY).Slice(boundsX, boundsWidth);

                    PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                    Numerics.Premultiply(sourceBuffer);

                    ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);
                    float factor = Unsafe.Add(ref kernelBase, kY);

                    for (int x = 0; x < sourceBuffer.Length; x++)
                    {
                        ref Vector4 target = ref Unsafe.Add(ref targetBase, x);
                        Vector4 sample = Unsafe.Add(ref sourceBase, x);

                        target += factor * sample;
                    }
                }

                Numerics.UnPremultiply(targetBuffer);

                Span<TPixel> targetRow = this.targetPixels.GetRowSpan(y).Slice(boundsX, boundsWidth);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetBuffer, targetRow);
            }
        }
    }
}

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
    /// Defines a processor that uses a 2 dimensional matrix to perform convolution against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ConvolutionProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConvolutionProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="kernelXY">The 2d gradient operator.</param>
        /// <param name="preserveAlpha">Whether the convolution filter is applied to alpha as well as the color channels.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public ConvolutionProcessor(
            Configuration configuration,
            in DenseMatrix<float> kernelXY,
            bool preserveAlpha,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.KernelXY = kernelXY;
            this.PreserveAlpha = preserveAlpha;
        }

        /// <summary>
        /// Gets the 2d convolution kernel.
        /// </summary>
        public DenseMatrix<float> KernelXY { get; }

        /// <summary>
        /// Gets a value indicating whether the convolution filter is applied to alpha as well as the color channels.
        /// </summary>
        public bool PreserveAlpha { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            MemoryAllocator allocator = this.Configuration.MemoryAllocator;
            using Buffer2D<TPixel> targetPixels = allocator.Allocate2D<TPixel>(source.Size());

            source.CopyTo(targetPixels);

            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());

            // We use a rectangle 2x the interest width to allocate a buffer big enough
            // for source and target bulk pixel conversion.
            var operationBounds = new Rectangle(interest.X, interest.Y, interest.Width * 2, interest.Height);
            using (var map = new KernelSamplingMap(allocator))
            {
                map.BuildSamplingOffsetMap(this.KernelXY, interest);

                var operation = new RowOperation(interest, targetPixels, source.PixelBuffer, map, this.KernelXY, this.Configuration, this.PreserveAlpha);
                ParallelRowIterator.IterateRows<RowOperation, Vector4>(
                   this.Configuration,
                   operationBounds,
                   in operation);
            }

            Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the convolution logic for <see cref="ConvolutionProcessor{T}"/>.
        /// </summary>
        private readonly struct RowOperation : IRowOperation<Vector4>
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Buffer2D<TPixel> sourcePixels;
            private readonly KernelSamplingMap map;
            private readonly DenseMatrix<float> kernel;
            private readonly Configuration configuration;
            private readonly bool preserveAlpha;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                Rectangle bounds,
                Buffer2D<TPixel> targetPixels,
                Buffer2D<TPixel> sourcePixels,
                KernelSamplingMap map,
                DenseMatrix<float> kernel,
                Configuration configuration,
                bool preserveAlpha)
            {
                this.bounds = bounds;
                this.targetPixels = targetPixels;
                this.sourcePixels = sourcePixels;
                this.map = map;
                this.kernel = kernel;
                this.configuration = configuration;
                this.preserveAlpha = preserveAlpha;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y, Span<Vector4> span)
            {
                // Span is 2x bounds.
                int boundsX = this.bounds.X;
                int boundsWidth = this.bounds.Width;
                Span<Vector4> sourceBuffer = span.Slice(0, this.bounds.Width);
                Span<Vector4> targetBuffer = span.Slice(this.bounds.Width);

                ref Vector4 targetRowRef = ref MemoryMarshal.GetReference(span);
                Span<TPixel> targetRowSpan = this.targetPixels.GetRowSpan(y).Slice(boundsX, boundsWidth);

                var state = new ConvolutionState(in this.kernel, this.map);
                int row = y - this.bounds.Y;
                ref int sampleRowBase = ref state.GetSampleRow(row);

                if (this.preserveAlpha)
                {
                    // Clear the target buffer for each row run.
                    targetBuffer.Clear();
                    ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);

                    Span<TPixel> sourceRow;
                    for (int kY = 0; kY < state.Kernel.Rows; kY++)
                    {
                        // Get the precalculated source sample row for this kernel row and copy to our buffer.
                        int offsetY = Unsafe.Add(ref sampleRowBase, kY);
                        sourceRow = this.sourcePixels.GetRowSpan(offsetY).Slice(boundsX, boundsWidth);
                        PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);

                        for (int x = 0; x < sourceBuffer.Length; x++)
                        {
                            ref int sampleColumnBase = ref state.GetSampleColumn(x);
                            ref Vector4 target = ref Unsafe.Add(ref targetBase, x);

                            for (int kX = 0; kX < state.Kernel.Columns; kX++)
                            {
                                int offsetX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                                Vector4 sample = Unsafe.Add(ref sourceBase, offsetX);
                                target += state.Kernel[kY, kX] * sample;
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
                }
                else
                {
                    // Clear the target buffer for each row run.
                    targetBuffer.Clear();
                    ref Vector4 targetBase = ref MemoryMarshal.GetReference(targetBuffer);

                    for (int kY = 0; kY < state.Kernel.Rows; kY++)
                    {
                        // Get the precalculated source sample row for this kernel row and copy to our buffer.
                        int offsetY = Unsafe.Add(ref sampleRowBase, kY);
                        Span<TPixel> sourceRow = this.sourcePixels.GetRowSpan(offsetY).Slice(boundsX, boundsWidth);
                        PixelOperations<TPixel>.Instance.ToVector4(this.configuration, sourceRow, sourceBuffer);

                        Numerics.Premultiply(sourceBuffer);
                        ref Vector4 sourceBase = ref MemoryMarshal.GetReference(sourceBuffer);

                        for (int x = 0; x < sourceBuffer.Length; x++)
                        {
                            ref int sampleColumnBase = ref state.GetSampleColumn(x);
                            ref Vector4 target = ref Unsafe.Add(ref targetBase, x);

                            for (int kX = 0; kX < state.Kernel.Columns; kX++)
                            {
                                int offsetX = Unsafe.Add(ref sampleColumnBase, kX) - boundsX;
                                Vector4 sample = Unsafe.Add(ref sourceBase, offsetX);
                                target += state.Kernel[kY, kX] * sample;
                            }
                        }
                    }

                    Numerics.UnPremultiply(targetBuffer);
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, targetBuffer, targetRowSpan);
            }
        }
    }
}

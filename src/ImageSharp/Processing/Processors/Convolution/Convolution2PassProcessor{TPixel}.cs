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
        /// Gets the horizontal gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
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

            using (var mapX = new KernelOffsetMap(this.Configuration.MemoryAllocator))
            {
                mapX.BuildOffsetMap(this.KernelX, interest);

                // Horizontal convolution
                var horizontalOperation = new RowOperation(
                    interest,
                    firstPassPixels,
                    source.PixelBuffer,
                    mapX,
                    this.KernelX,
                    this.Configuration,
                    this.PreserveAlpha);

                ParallelRowIterator.IterateRows<RowOperation, Vector4>(
                    this.Configuration,
                    interest,
                    in horizontalOperation);
            }

            using (var mapY = new KernelOffsetMap(this.Configuration.MemoryAllocator))
            {
                mapY.BuildOffsetMap(this.KernelY, interest);

                // Vertical convolution
                var verticalOperation = new RowOperation(
                    interest,
                    source.PixelBuffer,
                    firstPassPixels,
                    mapY,
                    this.KernelY,
                    this.Configuration,
                    this.PreserveAlpha);

                ParallelRowIterator.IterateRows<RowOperation, Vector4>(
                    this.Configuration,
                    interest,
                    in verticalOperation);
            }
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the convolution logic for <see cref="Convolution2PassProcessor{T}"/>.
        /// </summary>
        private readonly struct RowOperation : IRowOperation<Vector4>
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Buffer2D<TPixel> sourcePixels;
            private readonly KernelOffsetMap map;
            private readonly DenseMatrix<float> kernel;
            private readonly Configuration configuration;
            private readonly bool preserveAlpha;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                Rectangle bounds,
                Buffer2D<TPixel> targetPixels,
                Buffer2D<TPixel> sourcePixels,
                KernelOffsetMap map,
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
                ref Vector4 spanRef = ref MemoryMarshal.GetReference(span);
                Span<TPixel> targetRowSpan = this.targetPixels.GetRowSpan(y).Slice(this.bounds.X);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, targetRowSpan.Slice(0, span.Length), span);
                Span<int> yOffsets = this.map.GetYOffsetSpan();
                Span<int> xOffsets = this.map.GetXOffsetSpan();
                int row = y - this.bounds.Y;

                if (this.preserveAlpha)
                {
                    for (int column = 0; column < this.bounds.Width; column++)
                    {
                        DenseMatrixUtils.Convolve3(
                            in this.kernel,
                            yOffsets,
                            xOffsets,
                            this.sourcePixels,
                            ref spanRef,
                            row,
                            column);
                    }
                }
                else
                {
                    for (int column = 0; column < this.bounds.Width; column++)
                    {
                        DenseMatrixUtils.Convolve4(
                            in this.kernel,
                            yOffsets,
                            xOffsets,
                            this.sourcePixels,
                            ref spanRef,
                            row,
                            column);
                    }
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, targetRowSpan);
            }
        }
    }
}

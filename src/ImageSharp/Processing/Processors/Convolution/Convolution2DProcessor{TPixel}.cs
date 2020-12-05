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
    /// Defines a processor that uses two one-dimensional matrices to perform convolution against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class Convolution2DProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Convolution2DProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        /// <param name="preserveAlpha">Whether the convolution filter is applied to alpha as well as the color channels.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public Convolution2DProcessor(
            Configuration configuration,
            in DenseMatrix<float> kernelX,
            in DenseMatrix<float> kernelY,
            bool preserveAlpha,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            Guard.IsTrue(kernelX.Size.Equals(kernelY.Size), $"{nameof(kernelX)} {nameof(kernelY)}", "Kernel sizes must be the same.");
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
            MemoryAllocator allocator = this.Configuration.MemoryAllocator;
            using Buffer2D<TPixel> targetPixels = allocator.Allocate2D<TPixel>(source.Width, source.Height);

            source.CopyTo(targetPixels);

            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());
            using (var map = new KernelSamplingMap(allocator))
            {
                // Since the kernel sizes are identical we can use a single map.
                map.BuildSamplingOffsetMap(this.KernelY, interest);

                var operation = new RowOperation(
                    interest,
                    targetPixels,
                    source.PixelBuffer,
                    map,
                    this.KernelY,
                    this.KernelX,
                    this.Configuration,
                    this.PreserveAlpha);

                ParallelRowIterator.IterateRows<RowOperation, Vector4>(
                    this.Configuration,
                    interest,
                    in operation);
            }

            Buffer2D<TPixel>.SwapOrCopyContent(source.PixelBuffer, targetPixels);
        }

        /// <summary>
        /// A <see langword="struct"/> implementing the convolution logic for <see cref="Convolution2DProcessor{T}"/>.
        /// </summary>
        private readonly struct RowOperation : IRowOperation<Vector4>
        {
            private readonly Rectangle bounds;
            private readonly Buffer2D<TPixel> targetPixels;
            private readonly Buffer2D<TPixel> sourcePixels;
            private readonly KernelSamplingMap map;
            private readonly DenseMatrix<float> kernelY;
            private readonly DenseMatrix<float> kernelX;
            private readonly Configuration configuration;
            private readonly bool preserveAlpha;

            [MethodImpl(InliningOptions.ShortMethod)]
            public RowOperation(
                Rectangle bounds,
                Buffer2D<TPixel> targetPixels,
                Buffer2D<TPixel> sourcePixels,
                KernelSamplingMap map,
                DenseMatrix<float> kernelY,
                DenseMatrix<float> kernelX,
                Configuration configuration,
                bool preserveAlpha)
            {
                this.bounds = bounds;
                this.targetPixels = targetPixels;
                this.sourcePixels = sourcePixels;
                this.map = map;
                this.kernelY = kernelY;
                this.kernelX = kernelX;
                this.configuration = configuration;
                this.preserveAlpha = preserveAlpha;
            }

            /// <inheritdoc/>
            [MethodImpl(InliningOptions.ShortMethod)]
            public void Invoke(int y, Span<Vector4> span)
            {
                ref Vector4 targetRowRef = ref MemoryMarshal.GetReference(span);
                Span<TPixel> targetRowSpan = this.targetPixels.GetRowSpan(y).Slice(this.bounds.X);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, targetRowSpan.Slice(0, span.Length), span);

                var state = new Convolution2DState(this.kernelY, this.kernelX, this.map);
                int row = y - this.bounds.Y;

                if (this.preserveAlpha)
                {
                    for (int column = 0; column < this.bounds.Width; column++)
                    {
                        Convolver.Convolve2D3(
                            in state,
                            this.sourcePixels,
                            ref targetRowRef,
                            row,
                            column);
                    }
                }
                else
                {
                    for (int column = 0; column < this.bounds.Width; column++)
                    {
                        Convolver.Convolve2D4(
                            in state,
                            this.sourcePixels,
                            ref targetRowRef,
                            row,
                            column);
                    }
                }

                PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, span, targetRowSpan);
            }
        }
    }
}

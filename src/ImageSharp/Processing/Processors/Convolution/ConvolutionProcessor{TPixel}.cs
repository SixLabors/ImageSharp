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
            using (var map = new KernelSamplingMap(allocator))
            {
                map.BuildSamplingOffsetMap(this.KernelXY, interest);

                var operation = new RowOperation(interest, targetPixels, source.PixelBuffer, map, this.KernelXY, this.Configuration, this.PreserveAlpha);
                ParallelRowIterator.IterateRows<RowOperation, Vector4>(
                   this.Configuration,
                   interest,
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
                ref Vector4 targetRowRef = ref MemoryMarshal.GetReference(span);
                Span<TPixel> targetRowSpan = this.targetPixels.GetRowSpan(y).Slice(this.bounds.X);
                PixelOperations<TPixel>.Instance.ToVector4(this.configuration, targetRowSpan.Slice(0, span.Length), span);

                var state = new ConvolutionState(in this.kernel, this.map);
                int row = y - this.bounds.Y;

                if (this.preserveAlpha)
                {
                    for (int column = 0; column < this.bounds.Width; column++)
                    {
                        Convolver.Convolve3(
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
                        Convolver.Convolve4(
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

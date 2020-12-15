// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
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

            using (var mapX = new KernelSamplingMap(this.Configuration.MemoryAllocator))
            {
                mapX.BuildSamplingOffsetMap(this.KernelX, interest);

                // Horizontal convolution
                var horizontalOperation = new ConvolutionRowOperation<TPixel>(
                    interest,
                    firstPassPixels,
                    source.PixelBuffer,
                    mapX,
                    this.KernelX,
                    this.Configuration,
                    this.PreserveAlpha);

                ParallelRowIterator.IterateRows<ConvolutionRowOperation<TPixel>, Vector4>(
                    this.Configuration,
                    operationBounds,
                    in horizontalOperation);
            }

            using (var mapY = new KernelSamplingMap(this.Configuration.MemoryAllocator))
            {
                mapY.BuildSamplingOffsetMap(this.KernelY, interest);

                // Vertical convolution
                var verticalOperation = new ConvolutionRowOperation<TPixel>(
                    interest,
                    source.PixelBuffer,
                    firstPassPixels,
                    mapY,
                    this.KernelY,
                    this.Configuration,
                    this.PreserveAlpha);

                ParallelRowIterator.IterateRows<ConvolutionRowOperation<TPixel>, Vector4>(
                    this.Configuration,
                    operationBounds,
                    in verticalOperation);
            }
        }
    }
}

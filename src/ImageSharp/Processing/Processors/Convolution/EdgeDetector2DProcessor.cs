// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a processor that detects edges within an image using two one-dimensional matrices.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class EdgeDetector2DProcessor<TPixel> : ImageProcessor<TPixel>, IEdgeDetectorProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetector2DProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        protected EdgeDetector2DProcessor(DenseMatrix<float> kernelX, DenseMatrix<float> kernelY, bool grayscale)
        {
            this.KernelX = kernelX;
            this.KernelY = kernelY;
            this.Grayscale = grayscale;
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelY { get; }

        /// <inheritdoc/>
        public bool Grayscale { get; set; }

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            new Convolution2DProcessor<TPixel>(this.KernelX, this.KernelY).Apply(source, sourceRectangle, configuration);
        }

        /// <inheritdoc/>
        protected override void BeforeFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor<TPixel>(1F).Apply(source, sourceRectangle, configuration);
            }
        }
    }
}
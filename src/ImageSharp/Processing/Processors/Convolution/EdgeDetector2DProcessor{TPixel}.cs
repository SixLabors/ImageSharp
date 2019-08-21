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
    internal class EdgeDetector2DProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetector2DProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        internal EdgeDetector2DProcessor(
            in DenseMatrix<float> kernelX,
            in DenseMatrix<float> kernelY,
            bool grayscale,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(source, sourceRectangle)
        {
            Guard.IsTrue(kernelX.Size.Equals(kernelY.Size), $"{nameof(kernelX)} {nameof(kernelY)}", "Kernel sizes must be the same.");
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

        public bool Grayscale { get; }

        /// <inheritdoc/>
        protected override void BeforeImageApply()
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor(1F).Apply(this.Source, this.SourceRectangle);
            }
        }

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source)
            => new Convolution2DProcessor<TPixel>(this.KernelX, this.KernelY, true, this.Source, this.SourceRectangle).Apply(source);
    }
}

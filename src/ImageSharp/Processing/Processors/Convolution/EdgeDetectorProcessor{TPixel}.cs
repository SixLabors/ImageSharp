// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a processor that detects edges within an image using a single two dimensional matrix.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class EdgeDetectorProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="kernelXY">The 2d gradient operator.</param>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The target area to process for the current processor instance.</param>
        public EdgeDetectorProcessor(
            Configuration configuration,
            in DenseMatrix<float> kernelXY,
            bool grayscale,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.KernelXY = kernelXY;
            this.Grayscale = grayscale;
        }

        public bool Grayscale { get; }

        /// <summary>
        /// Gets the 2d gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelXY { get; }

        /// <inheritdoc/>
        protected override void BeforeImageApply()
        {
            if (this.Grayscale)
            {
                new GrayscaleBt709Processor(1F).Execute(this.Configuration, this.Source, this.SourceRectangle);
            }

            base.BeforeImageApply();
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            using (var processor = new ConvolutionProcessor<TPixel>(this.Configuration, this.KernelXY, true, this.Source, this.SourceRectangle))
            {
                processor.Apply(source);
            }
        }
    }
}

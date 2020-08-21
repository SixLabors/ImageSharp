// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a processor that detects edges within an image using a single two dimensional matrix.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class EdgeDetectorProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly bool grayscale;
        private readonly DenseMatrix<float> kernelXY;

        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="EdgeDetectorProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The target area to process for the current processor instance.</param>
        public EdgeDetectorProcessor(
            Configuration configuration,
            EdgeDetectorProcessor definition,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.kernelXY = definition.Kernel.KernelXY;
            this.grayscale = definition.Grayscale;
        }

        /// <inheritdoc/>
        protected override void BeforeImageApply()
        {
            using (IImageProcessor<TPixel> opaque = new OpaqueProcessor<TPixel>(this.Configuration, this.Source, this.SourceRectangle))
            {
                opaque.Execute();
            }

            if (this.grayscale)
            {
                new GrayscaleBt709Processor(1F).Execute(this.Configuration, this.Source, this.SourceRectangle);
            }

            base.BeforeImageApply();
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            using var processor = new ConvolutionProcessor<TPixel>(this.Configuration, in this.kernelXY, true, this.Source, this.SourceRectangle);
            processor.Apply(source);
        }
    }
}

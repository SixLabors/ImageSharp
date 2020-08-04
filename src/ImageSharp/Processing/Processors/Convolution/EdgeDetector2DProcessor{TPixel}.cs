// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines a processor that detects edges within an image using two one-dimensional matrices.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class EdgeDetector2DProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly DenseMatrix<float> kernelX;
        private readonly DenseMatrix<float> kernelY;
        private readonly bool grayscale;

        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetector2DProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="EdgeDetector2DProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public EdgeDetector2DProcessor(
            Configuration configuration,
            EdgeDetector2DProcessor definition,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.kernelX = definition.Kernel.KernelX;
            this.kernelY = definition.Kernel.KernelY;
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

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            using var processor = new Convolution2DProcessor<TPixel>(
                this.Configuration,
                in this.kernelX,
                in this.kernelY,
                true,
                this.Source,
                this.SourceRectangle);

            processor.Apply(source);
        }
    }
}

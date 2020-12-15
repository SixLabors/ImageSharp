// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies Gaussian sharpening processing to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GaussianSharpenProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianSharpenProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="GaussianBlurProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public GaussianSharpenProcessor(
            Configuration configuration,
            GaussianSharpenProcessor definition,
            Image<TPixel> source,
            Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            int kernelSize = (definition.Radius * 2) + 1;
            this.Kernel = ConvolutionProcessorHelpers.CreateGaussianSharpenKernel(kernelSize, definition.Sigma);
        }

        /// <summary>
        /// Gets the 1D convolution kernel.
        /// </summary>
        public float[] Kernel { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            using var processor = new Convolution2PassProcessor<TPixel>(this.Configuration, this.Kernel, false, this.Source, this.SourceRectangle);

            processor.Apply(source);
        }
    }
}

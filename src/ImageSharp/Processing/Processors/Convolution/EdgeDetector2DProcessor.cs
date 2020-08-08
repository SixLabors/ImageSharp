// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection using the two 1D gradient operators.
    /// </summary>
    public sealed class EdgeDetector2DProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetector2DProcessor"/> class.
        /// </summary>
        /// <param name="kernel">The 2D edge detector kernel.</param>
        /// <param name="grayscale">
        /// Whether to convert the image to grayscale before performing edge detection.
        /// </param>
        public EdgeDetector2DProcessor(EdgeDetector2DKernel kernel, bool grayscale)
        {
            this.Kernel = kernel;
            this.Grayscale = grayscale;
        }

        /// <summary>
        /// Gets the 2D edge detector kernel.
        /// </summary>
        public EdgeDetector2DKernel Kernel { get; }

        /// <summary>
        /// Gets a value indicating whether to convert the image to grayscale before performing
        /// edge detection.
        /// </summary>
        public bool Grayscale { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new EdgeDetector2DProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}

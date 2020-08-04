// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection using a single 2D gradient operator.
    /// </summary>
    public sealed class EdgeDetectorProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorProcessor"/> class.
        /// </summary>
        /// <param name="kernel">The  edge detector kernel.</param>
        /// <param name="grayscale">
        /// Whether to convert the image to grayscale before performing edge detection.
        /// </param>
        public EdgeDetectorProcessor(EdgeDetectorKernel kernel, bool grayscale)
        {
            this.Kernel = kernel;
            this.Grayscale = grayscale;
        }

        /// <summary>
        /// Gets the edge detector kernel.
        /// </summary>
        public EdgeDetectorKernel Kernel { get; }

        /// <summary>
        /// Gets a value indicating whether to convert the image to grayscale before performing
        /// edge detection.
        /// </summary>
        public bool Grayscale { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new EdgeDetectorProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}

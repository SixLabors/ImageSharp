// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection using eight gradient operators.
    /// </summary>
    public sealed class EdgeDetectorCompassProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorCompassProcessor"/> class.
        /// </summary>
        /// <param name="kernel">The edge detector kernel.</param>
        /// <param name="grayscale">
        /// Whether to convert the image to grayscale before performing edge detection.
        /// </param>
        public EdgeDetectorCompassProcessor(EdgeDetectorCompassKernel kernel, bool grayscale)
        {
            this.Kernel = kernel;
            this.Grayscale = grayscale;
        }

        /// <summary>
        /// Gets the edge detector kernel.
        /// </summary>
        public EdgeDetectorCompassKernel Kernel { get; }

        /// <summary>
        /// Gets a value indicating whether to convert the image to grayscale before performing
        /// edge detection.
        /// </summary>
        public bool Grayscale { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            where TPixel : unmanaged, IPixel<TPixel>
            => new EdgeDetectorCompassProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}

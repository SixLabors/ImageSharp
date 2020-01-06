// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection using the Sobel operator filter.
    /// See <see href="http://en.wikipedia.org/wiki/Sobel_operator"/>.
    /// </summary>
    public sealed class SobelProcessor : EdgeDetectorProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SobelProcessor"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public SobelProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new EdgeDetector2DProcessor<TPixel>(
                configuration,
                SobelKernels.SobelX,
                SobelKernels.SobelY,
                this.Grayscale,
                source,
                sourceRectangle);
    }
}

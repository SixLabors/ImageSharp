// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection using the Prewitt operator filter.
    /// See <see href="http://en.wikipedia.org/wiki/Prewitt_operator"/>.
    /// </summary>
    public sealed class PrewittProcessor : EdgeDetectorProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrewittProcessor"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public PrewittProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new EdgeDetector2DProcessor<TPixel>(
                configuration,
                PrewittKernels.PrewittX,
                PrewittKernels.PrewittY,
                this.Grayscale,
                source,
                sourceRectangle);
    }
}

// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new EdgeDetector2DProcessor<TPixel>(PrewittKernels.PrewittX, PrewittKernels.PrewittY, this.Grayscale);
        }
    }
}
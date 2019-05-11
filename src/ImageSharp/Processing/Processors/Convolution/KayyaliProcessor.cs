// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection processing using the Kayyali operator filter.
    /// See <see href="http://edgedetection.webs.com/"/>.
    /// </summary>
    public sealed class KayyaliProcessor : EdgeDetectorProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KayyaliProcessor"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public KayyaliProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new EdgeDetector2DProcessor<TPixel>(
                KayyaliKernels.KayyaliX,
                KayyaliKernels.KayyaliY,
                this.Grayscale);
        }
    }
}
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection using the Kirsch operator filter.
    /// See <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>.
    /// </summary>
    public sealed class KirschProcessor : EdgeDetectorProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KirschProcessor"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public KirschProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new EdgeDetectorCompassProcessor<TPixel>(new KirschKernels(), this.Grayscale);
        }
    }
}
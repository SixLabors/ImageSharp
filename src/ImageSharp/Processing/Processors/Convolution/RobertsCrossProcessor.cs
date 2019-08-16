// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Defines edge detection processing using the Roberts Cross operator filter.
    /// See <see href="http://en.wikipedia.org/wiki/Roberts_cross"/>.
    /// </summary>
    public sealed class RobertsCrossProcessor : EdgeDetectorProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RobertsCrossProcessor"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public RobertsCrossProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new EdgeDetector2DProcessor<TPixel>(RobertsCrossKernels.RobertsCrossX, RobertsCrossKernels.RobertsCrossY, this.Grayscale);
        }
    }
}
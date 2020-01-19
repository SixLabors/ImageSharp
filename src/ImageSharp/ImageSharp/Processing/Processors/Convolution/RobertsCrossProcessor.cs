// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;

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
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle)
            => new EdgeDetector2DProcessor<TPixel>(
                configuration,
                RobertsCrossKernels.RobertsCrossX,
                RobertsCrossKernels.RobertsCrossY,
                this.Grayscale,
                source,
                sourceRectangle);
    }
}

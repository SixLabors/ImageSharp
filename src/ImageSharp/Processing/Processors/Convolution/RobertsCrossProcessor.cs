// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies edge detection processing to the image using the Roberts Cross operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Roberts_cross"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class RobertsCrossProcessor<TPixel> : EdgeDetector2DProcessor<TPixel>
         where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RobertsCrossProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public RobertsCrossProcessor(bool grayscale)
            : base(RobertsCrossKernels.RobertsCrossX, RobertsCrossKernels.RobertsCrossY, grayscale)
        {
        }
    }
}
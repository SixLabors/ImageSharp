// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies edge detection processing to the image using the Scharr operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Sobel_operator#Alternative_operators"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class ScharrProcessor<TPixel> : EdgeDetector2DProcessor<TPixel>
          where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScharrProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public ScharrProcessor(bool grayscale)
            : base(ScharrKernels.ScharrX, ScharrKernels.ScharrY, grayscale)
        {
        }
    }
}
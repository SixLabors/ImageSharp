// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies edge detection processing to the image using the Prewitt operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Prewitt_operator"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class PrewittProcessor<TPixel> : EdgeDetector2DProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrewittProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public PrewittProcessor(bool grayscale)
            : base(PrewittKernels.PrewittX, PrewittKernels.PrewittY, grayscale)
        {
        }
    }
}
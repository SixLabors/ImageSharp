// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies edge detection processing to the image using the Laplacian of Gaussian operator filter.
    /// <see href="http://fourier.eng.hmc.edu/e161/lectures/gradient/node8.html"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class LaplacianOfGaussianProcessor<TPixel> : EdgeDetectorProcessor<TPixel>
          where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LaplacianOfGaussianProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public LaplacianOfGaussianProcessor(bool grayscale)
            : base(LaplacianKernels.LaplacianOfGaussianXY, grayscale)
        {
        }
    }
}
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies edge detection processing to the image using the Laplacian of Gaussian operator filter.
    /// See <see href="http://fourier.eng.hmc.edu/e161/lectures/gradient/node8.html"/>.
    /// </summary>
    public sealed class LaplacianOfGaussianProcessor : EdgeDetectorProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LaplacianOfGaussianProcessor"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public LaplacianOfGaussianProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new EdgeDetectorProcessor<TPixel>(LaplacianKernels.LaplacianOfGaussianXY, this.Grayscale);
        }
    }
}
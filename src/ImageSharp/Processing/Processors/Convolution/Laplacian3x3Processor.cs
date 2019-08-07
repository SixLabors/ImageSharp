// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies edge detection processing to the image using the Laplacian 3x3 operator filter.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// </summary>
    public sealed class Laplacian3x3Processor : EdgeDetectorProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Laplacian3x3Processor"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public Laplacian3x3Processor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
        {
            return new EdgeDetectorProcessor<TPixel>(LaplacianKernels.Laplacian3x3, this.Grayscale);
        }
    }
}

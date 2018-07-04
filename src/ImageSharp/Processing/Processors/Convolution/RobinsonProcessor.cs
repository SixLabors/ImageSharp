// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies edge detection processing to the image using the Robinson operator filter.
    /// <see href="http://www.tutorialspoint.com/dip/Robinson_Compass_Mask.htm"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class RobinsonProcessor<TPixel> : EdgeDetectorCompassProcessor<TPixel>
         where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RobinsonProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public RobinsonProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc/>
        public override DenseMatrix<float> North => RobinsonKernels.RobinsonNorth;

        /// <inheritdoc/>
        public override DenseMatrix<float> NorthWest => RobinsonKernels.RobinsonNorthWest;

        /// <inheritdoc/>
        public override DenseMatrix<float> West => RobinsonKernels.RobinsonWest;

        /// <inheritdoc/>
        public override DenseMatrix<float> SouthWest => RobinsonKernels.RobinsonSouthWest;

        /// <inheritdoc/>
        public override DenseMatrix<float> South => RobinsonKernels.RobinsonSouth;

        /// <inheritdoc/>
        public override DenseMatrix<float> SouthEast => RobinsonKernels.RobinsonSouthEast;

        /// <inheritdoc/>
        public override DenseMatrix<float> East => RobinsonKernels.RobinsonEast;

        /// <inheritdoc/>
        public override DenseMatrix<float> NorthEast => RobinsonKernels.RobinsonNorthEast;
    }
}
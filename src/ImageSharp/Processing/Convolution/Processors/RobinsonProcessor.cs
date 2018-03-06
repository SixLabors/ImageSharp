// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Convolution.Processors
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
        public override Fast2DArray<float> North => RobinsonKernels.RobinsonNorth;

        /// <inheritdoc/>
        public override Fast2DArray<float> NorthWest => RobinsonKernels.RobinsonNorthWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> West => RobinsonKernels.RobinsonWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> SouthWest => RobinsonKernels.RobinsonSouthWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> South => RobinsonKernels.RobinsonSouth;

        /// <inheritdoc/>
        public override Fast2DArray<float> SouthEast => RobinsonKernels.RobinsonSouthEast;

        /// <inheritdoc/>
        public override Fast2DArray<float> East => RobinsonKernels.RobinsonEast;

        /// <inheritdoc/>
        public override Fast2DArray<float> NorthEast => RobinsonKernels.RobinsonNorthEast;
    }
}
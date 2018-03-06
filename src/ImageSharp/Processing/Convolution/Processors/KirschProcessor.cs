// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Convolution.Processors
{
    /// <summary>
    /// Applies edge detection processing to the image using the Kirsch operator filter. <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class KirschProcessor<TPixel> : EdgeDetectorCompassProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KirschProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="grayscale">Whether to convert the image to grayscale before performing edge detection.</param>
        public KirschProcessor(bool grayscale)
            : base(grayscale)
        {
        }

        /// <inheritdoc/>
        public override Fast2DArray<float> North => KirshKernels.KirschNorth;

        /// <inheritdoc/>
        public override Fast2DArray<float> NorthWest => KirshKernels.KirschNorthWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> West => KirshKernels.KirschWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> SouthWest => KirshKernels.KirschSouthWest;

        /// <inheritdoc/>
        public override Fast2DArray<float> South => KirshKernels.KirschSouth;

        /// <inheritdoc/>
        public override Fast2DArray<float> SouthEast => KirshKernels.KirschSouthEast;

        /// <inheritdoc/>
        public override Fast2DArray<float> East => KirshKernels.KirschEast;

        /// <inheritdoc/>
        public override Fast2DArray<float> NorthEast => KirshKernels.KirschNorthEast;
    }
}
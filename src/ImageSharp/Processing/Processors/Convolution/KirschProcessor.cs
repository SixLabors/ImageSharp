// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
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
        public override DenseMatrix<float> North => KirschKernels.KirschNorth;

        /// <inheritdoc/>
        public override DenseMatrix<float> NorthWest => KirschKernels.KirschNorthWest;

        /// <inheritdoc/>
        public override DenseMatrix<float> West => KirschKernels.KirschWest;

        /// <inheritdoc/>
        public override DenseMatrix<float> SouthWest => KirschKernels.KirschSouthWest;

        /// <inheritdoc/>
        public override DenseMatrix<float> South => KirschKernels.KirschSouth;

        /// <inheritdoc/>
        public override DenseMatrix<float> SouthEast => KirschKernels.KirschSouthEast;

        /// <inheritdoc/>
        public override DenseMatrix<float> East => KirschKernels.KirschEast;

        /// <inheritdoc/>
        public override DenseMatrix<float> NorthEast => KirschKernels.KirschNorthEast;
    }
}
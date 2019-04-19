// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies box blur processing to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BoxBlurProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The maximum size of the kernel in either direction.
        /// </summary>
        private readonly int kernelSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoxBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public BoxBlurProcessor(int radius = 7)
        {
            this.Radius = radius;
            this.kernelSize = (radius * 2) + 1;
            this.KernelX = this.CreateBoxKernel();
            this.KernelY = this.KernelX.Transpose();
        }

        /// <summary>
        /// Gets the Radius
        /// </summary>
        public int Radius { get; }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelY { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration) => new Convolution2PassProcessor<TPixel>(this.KernelX, this.KernelY).Apply(source, sourceRectangle, configuration);

        /// <summary>
        /// Create a 1 dimensional Box kernel.
        /// </summary>
        /// <returns>The <see cref="DenseMatrix{T}"/></returns>
        private DenseMatrix<float> CreateBoxKernel()
        {
            int size = this.kernelSize;
            var kernel = new DenseMatrix<float>(size, 1);

            kernel.Fill(1F / size);

            return kernel;
        }
    }
}
// // Copyright (c) Six Labors and contributors.
// // Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies Gaussian blur processing to an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GaussianBlurProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly GaussianBlurProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="definition">The <see cref="GaussianBlurProcessor"/> defining the processor parameters.</param>
        public GaussianBlurProcessor(GaussianBlurProcessor definition)
        {
            this.definition = definition;
            int kernelSize = (definition.Radius * 2) + 1;
            this.KernelX = CreateGaussianKernel(kernelSize, definition.Sigma);
            this.KernelY = this.KernelX.Transpose();
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelY { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration) =>
            new Convolution2PassProcessor<TPixel>(this.KernelX, this.KernelY, false).Apply(
                source,
                sourceRectangle,
                configuration);

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function
        /// </summary>
        /// <returns>The <see cref="DenseMatrix{T}"/></returns>
        private static DenseMatrix<float> CreateGaussianKernel(int size, float weight)
        {
            var kernel = new DenseMatrix<float>(size, 1);

            float sum = 0F;
            float midpoint = (size - 1) / 2F;

            for (int i = 0; i < size; i++)
            {
                float x = i - midpoint;
                float gx = ImageMaths.Gaussian(x, weight);
                sum += gx;
                kernel[0, i] = gx;
            }

            // Normalize kernel so that the sum of all weights equals 1
            for (int i = 0; i < size; i++)
            {
                kernel[0, i] /= sum;
            }

            return kernel;
        }
    }
}
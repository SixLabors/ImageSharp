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
        private readonly BoxBlurProcessor definition;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoxBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="definition">The <see cref="BoxBlurProcessor"/> defining the processor parameters.</param>
        public BoxBlurProcessor(BoxBlurProcessor definition)
        {
            this.definition = definition;
            int kernelSize = (definition.Radius * 2) + 1;
            this.KernelX = CreateBoxKernel(kernelSize);
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
        /// Create a 1 dimensional Box kernel.
        /// </summary>
        /// <param name="kernelSize">The maximum size of the kernel in either direction.</param>
        /// <returns>The <see cref="DenseMatrix{T}"/>.</returns>
        private static DenseMatrix<float> CreateBoxKernel(int kernelSize)
        {
            var kernel = new DenseMatrix<float>(kernelSize, 1);
            kernel.Fill(1F / kernelSize);
            return kernel;
        }
    }
}
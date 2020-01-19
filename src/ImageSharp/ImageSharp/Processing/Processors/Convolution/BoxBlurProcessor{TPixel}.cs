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
        /// Initializes a new instance of the <see cref="BoxBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="BoxBlurProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public BoxBlurProcessor(Configuration configuration, BoxBlurProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
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
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            using (var processor = new Convolution2PassProcessor<TPixel>(this.Configuration, this.KernelX, this.KernelY, false, this.Source, this.SourceRectangle))
            {
                processor.Apply(source);
            }
        }

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

// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Applies box blur processing to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class BoxBlurProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
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
            this.Kernel = CreateBoxKernel(kernelSize);
        }

        /// <summary>
        /// Gets the 1D convolution kernel.
        /// </summary>
        public float[] Kernel { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            using var processor = new Convolution2PassProcessor<TPixel>(this.Configuration, this.Kernel, false, this.Source, this.SourceRectangle);

            processor.Apply(source);
        }

        /// <summary>
        /// Create a 1 dimensional Box kernel.
        /// </summary>
        /// <param name="kernelSize">The maximum size of the kernel in either direction.</param>
        /// <returns>The <see cref="DenseMatrix{T}"/>.</returns>
        private static float[] CreateBoxKernel(int kernelSize)
        {
            var kernel = new float[kernelSize];

            kernel.AsSpan().Fill(1F / kernelSize);

            return kernel;
        }
    }
}

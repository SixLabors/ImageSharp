// <copyright file="GaussianBlurProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Processing.Processors
{
    using System;

    using ImageSharp.Memory;
    using ImageSharp.PixelFormats;
    using SixLabors.Primitives;

    /// <summary>
    /// Applies a Gaussian blur sampler to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class GaussianBlurProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The maximum size of the kernel in either direction.
        /// </summary>
        private readonly int kernelSize;

        /// <summary>
        /// The spread of the blur.
        /// </summary>
        private readonly float sigma;

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sigma">The 'sigma' value representing the weight of the blur.</param>
        public GaussianBlurProcessor(float sigma = 3f)
        {
            this.kernelSize = ((int)Math.Ceiling(sigma) * 2) + 1;
            this.sigma = sigma;
            this.KernelX = this.CreateGaussianKernel(true);
            this.KernelY = this.CreateGaussianKernel(false);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public GaussianBlurProcessor(int radius)
        {
            this.kernelSize = (radius * 2) + 1;
            this.sigma = radius;
            this.KernelX = this.CreateGaussianKernel(true);
            this.KernelY = this.CreateGaussianKernel(false);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GaussianBlurProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="sigma">
        /// The 'sigma' value representing the weight of the blur.
        /// </param>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// This should be at least twice the sigma value.
        /// </param>
        public GaussianBlurProcessor(float sigma, int radius)
        {
            this.kernelSize = (radius * 2) + 1;
            this.sigma = sigma;
            this.KernelX = this.CreateGaussianKernel(true);
            this.KernelY = this.CreateGaussianKernel(false);
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public Fast2DArray<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public Fast2DArray<float> KernelY { get; }

        /// <inheritdoc/>
        protected override void OnApply(ImageBase<TPixel> source, Rectangle sourceRectangle)
        {
            new Convolution2PassProcessor<TPixel>(this.KernelX, this.KernelY).Apply(source, sourceRectangle);
        }

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function
        /// </summary>
        /// <param name="horizontal">Whether to calculate a horizontal kernel.</param>
        /// <returns>The <see cref="Fast2DArray{T}"/></returns>
        private Fast2DArray<float> CreateGaussianKernel(bool horizontal)
        {
            int size = this.kernelSize;
            float weight = this.sigma;
            Fast2DArray<float> kernel = horizontal
                ? new Fast2DArray<float>(size, 1)
                : new Fast2DArray<float>(1, size);

            float sum = 0F;
            float midpoint = (size - 1) / 2F;

            for (int i = 0; i < size; i++)
            {
                float x = i - midpoint;
                float gx = ImageMaths.Gaussian(x, weight);
                sum += gx;
                if (horizontal)
                {
                    kernel[0, i] = gx;
                }
                else
                {
                    kernel[i, 0] = gx;
                }
            }

            // Normalize kernel so that the sum of all weights equals 1
            if (horizontal)
            {
                for (int i = 0; i < size; i++)
                {
                    kernel[0, i] = kernel[0, i] / sum;
                }
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    kernel[i, 0] = kernel[i, 0] / sum;
                }
            }

            return kernel;
        }
    }
}
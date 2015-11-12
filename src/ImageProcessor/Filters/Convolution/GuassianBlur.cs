// <copyright file="GuassianBlur.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;

    /// <summary>
    /// Applies a Gaussian blur filter to the image.
    /// </summary>
    public class GuassianBlur : Convolution2PassFilter
    {
        /// <summary>
        /// The maximum size of the kernal in either direction.
        /// </summary>
        private readonly int kernelSize;

        /// <summary>
        /// The spread of the blur.
        /// </summary>
        private readonly float sigma;

        /// <summary>
        /// The vertical kernel
        /// </summary>
        private float[,] kernelY;

        /// <summary>
        /// The horizontal kernel
        /// </summary>
        private float[,] kernelX;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuassianBlur"/> class.
        /// </summary>
        /// <param name="sigma">
        /// The 'sigma' value representing the weight of the blur.
        /// </param>
        public GuassianBlur(float sigma = 3f)
        {
            this.kernelSize = ((int)Math.Ceiling(sigma) * 2) + 1;
            this.sigma = sigma;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuassianBlur"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public GuassianBlur(int radius)
        {
            this.kernelSize = (radius * 2) + 1;
            this.sigma = radius;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuassianBlur"/> class.
        /// </summary>
        /// <param name="sigma">
        /// The 'sigma' value representing the weight of the blur.
        /// </param>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// This should be at least twice the sigma value.
        /// </param>
        public GuassianBlur(float sigma, int radius)
        {
            this.kernelSize = (radius * 2) + 1;
            this.sigma = sigma;
        }

        /// <inheritdoc/>
        public override float[,] KernelX => this.kernelX;

        /// <inheritdoc/>
        public override float[,] KernelY => this.kernelY;

        /// <inheritdoc/>
        public override int Parallelism => 1;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.kernelY == null)
            {
                this.kernelY = this.CreateGaussianKernel(false);
            }

            if (this.kernelX == null)
            {
                this.kernelX = this.CreateGaussianKernel(true);
            }
        }

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function
        /// </summary>
        /// <param name="horizontal">Whether to calculate a horizontal kernel.</param>
        /// <returns>The <see cref="T:float[,]"/></returns>
        private float[,] CreateGaussianKernel(bool horizontal)
        {
            int size = this.kernelSize;
            float weight = this.sigma;
            float[,] kernel = horizontal ? new float[1, size] : new float[size, 1];
            float sum = 0.0f;

            float midpoint = (size - 1) / 2f;
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

            // Normalise kernel so that the sum of all weights equals 1
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

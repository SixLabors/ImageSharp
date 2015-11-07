// <copyright file="GuassianSharpen.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;

    /// <summary>
    /// Applies a Gaussian sharpening filter to the image.
    /// </summary>
    public class GuassianSharpen : Convolution2PassFilter
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
        /// Initializes a new instance of the <see cref="GuassianSharpen"/> class.
        /// </summary>
        /// <param name="sigma">
        /// The 'sigma' value representing the weight of the sharpening.
        /// </param>
        public GuassianSharpen(float sigma = 3f)
        {
            this.kernelSize = ((int)Math.Ceiling(sigma) * 2) + 1;
            this.sigma = sigma;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuassianSharpen"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public GuassianSharpen(int radius)
        {
            this.kernelSize = (radius * 2) + 1;
            this.sigma = radius;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GuassianSharpen"/> class.
        /// </summary>
        /// <param name="sigma">
        /// The 'sigma' value representing the weight of the sharpen.
        /// </param>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// This should be at least twice the sigma value.
        /// </param>
        public GuassianSharpen(float sigma, int radius)
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
        protected override void OnApply(Rectangle targetRectangle, Rectangle sourceRectangle)
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
            float sum = 0;

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

            // Invert the kernel for sharpening.
            int midpointRounded = (int)midpoint;

            if (horizontal)
            {
                for (int i = 0; i < size; i++)
                {
                    if (i == midpointRounded)
                    {
                        // Calculate central value
                        kernel[0, i] = (2f * sum) - kernel[0, i];
                    }
                    else
                    {
                        // invert value
                        kernel[0, i] = -kernel[0, i];
                    }
                }
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    if (i == midpointRounded)
                    {
                        // Calculate central value
                        kernel[i, 0] = (2 * sum) - kernel[i, 0];
                    }
                    else
                    {
                        // invert value
                        kernel[i, 0] = -kernel[i, 0];
                    }
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

// <copyright file="GuassianBlur.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Filters
{
    using System;

    /// <summary>
    /// Applies a Gaussian blur to the image.
    /// TODO: Something is not right here. The output blur is more like a motion blur.
    /// </summary>
    public class GuassianBlur : Convolution2DFilter
    {
        /// <summary>
        /// The maximum size of the kernal in either direction.
        /// </summary>
        private readonly int kernelSize;

        /// <summary>
        /// The standard deviation (weight)
        /// </summary>
        private readonly float standardDeviation;

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
        /// <param name="standardDeviation">
        /// The standard deviation 'sigma' value for calculating Gaussian curves.
        /// </param>
        public GuassianBlur(float standardDeviation = 3f)
        {
            this.kernelSize = ((int)Math.Ceiling(standardDeviation) * 2) + 1;
            this.standardDeviation = standardDeviation;
        }

        /// <inheritdoc/>
        public override float[,] KernelX => this.kernelX;

        /// <inheritdoc/>
        public override float[,] KernelY => this.kernelY;

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
            float[,] kernel = horizontal ? new float[1, size] : new float[size, 1];
            float sum = 0.0f;

            float midpoint = (size - 1) / 2f;
            for (int i = 0; i < size; i++)
            {
                float x = i - midpoint;
                float gx = this.Gaussian(x);
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

        /// <summary>
        /// Implementation of 1D Gaussian G(x) function
        /// </summary>
        /// <param name="x">The x provided to G(x)</param>
        /// <returns>The Gaussian G(x)</returns>
        private float Gaussian(float x)
        {
            const float Numerator = 1.0f;
            float deviation = this.standardDeviation;
            float denominator = (float)(Math.Sqrt(2 * Math.PI) * deviation);

            float exponentNumerator = -x * x;
            float exponentDenominator = (float)(2 * Math.Pow(deviation, 2));

            float left = Numerator / denominator;
            float right = (float)Math.Exp(exponentNumerator / exponentDenominator);

            return left * right;
        }
    }
}

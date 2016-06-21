// <copyright file="BoxBlur.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Filters
{
    /// <summary>
    /// Applies a Box blur filter to the image.
    /// </summary>
    public class BoxBlur : Convolution2PassFilter
    {
        /// <summary>
        /// The maximum size of the kernal in either direction.
        /// </summary>
        private readonly int kernelSize;

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
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public BoxBlur(int radius = 7)
        {
            this.kernelSize = (radius * 2) + 1;
        }

        /// <inheritdoc/>
        public override float[,] KernelX => this.kernelX;

        /// <inheritdoc/>
        public override float[,] KernelY => this.kernelY;

        /// <inheritdoc/>
        public override int Parallelism => 1;

        /// <inheritdoc/>
        protected override void OnApply(ImageBase target, ImageBase source, Rectangle targetRectangle, Rectangle sourceRectangle)
        {
            if (this.kernelY == null)
            {
                this.kernelY = this.CreateBoxKernel(false);
            }

            if (this.kernelX == null)
            {
                this.kernelX = this.CreateBoxKernel(true);
            }
        }

        /// <summary>
        /// Create a 1 dimensional Box kernel.
        /// </summary>
        /// <param name="horizontal">Whether to calculate a horizontal kernel.</param>
        /// <returns>The <see cref="T:float[,]"/></returns>
        private float[,] CreateBoxKernel(bool horizontal)
        {
            int size = this.kernelSize;
            float[,] kernel = horizontal ? new float[1, size] : new float[size, 1];
            float sum = 0.0f;

            for (int i = 0; i < size; i++)
            {
                float x = 1;
                sum += x;
                if (horizontal)
                {
                    kernel[0, i] = x;
                }
                else
                {
                    kernel[i, 0] = x;
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

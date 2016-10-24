// <copyright file="BoxBlurProcessor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Processors
{
    /// <summary>
    /// Applies a Box blur filter to the image.
    /// </summary>
    /// <typeparam name="TColor">The pixel format.</typeparam>
    /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
    public class BoxBlurProcessor<TColor, TPacked> : ImageSampler<TColor, TPacked>
        where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
    {
        /// <summary>
        /// The maximum size of the kernel in either direction.
        /// </summary>
        private readonly int kernelSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="BoxBlurProcessor{TColor, TPacked}"/> class.
        /// </summary>
        /// <param name="radius">
        /// The 'radius' value representing the size of the area to sample.
        /// </param>
        public BoxBlurProcessor(int radius = 7)
        {
            this.kernelSize = (radius * 2) + 1;
            this.KernelX = this.CreateBoxKernel(true);
            this.KernelY = this.CreateBoxKernel(false);
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public float[][] KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public float[][] KernelY { get; }

        /// <inheritdoc/>
        public override void Apply(ImageBase<TColor, TPacked> target, ImageBase<TColor, TPacked> source, Rectangle targetRectangle, Rectangle sourceRectangle, int startY, int endY)
        {
            new Convolution2PassFilter<TColor, TPacked>(this.KernelX, this.KernelY).Apply(target, source, targetRectangle, sourceRectangle, startY, endY);
        }

        /// <summary>
        /// Create a 1 dimensional Box kernel.
        /// </summary>
        /// <param name="horizontal">Whether to calculate a horizontal kernel.</param>
        /// <returns>The <see cref="T:float[][]"/></returns>
        private float[][] CreateBoxKernel(bool horizontal)
        {
            int size = this.kernelSize;
            float[][] kernel = horizontal ? new float[1][] : new float[size][];

            if (horizontal)
            {
                kernel[0] = new float[size];
            }

            float sum = 0.0f;

            for (int i = 0; i < size; i++)
            {
                float x = 1;
                sum += x;
                if (horizontal)
                {
                    kernel[0][i] = x;
                }
                else
                {
                    kernel[i] = new[] { x };
                }
            }

            // Normalise kernel so that the sum of all weights equals 1
            if (horizontal)
            {
                for (int i = 0; i < size; i++)
                {
                    kernel[0][i] = kernel[0][i] / sum;
                }
            }
            else
            {
                for (int i = 0; i < size; i++)
                {
                    kernel[i][0] = kernel[i][0] / sum;
                }
            }

            return kernel;
        }
    }
}
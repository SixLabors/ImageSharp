// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    internal static class ConvolutionProcessorHelpers
    {
        /// <summary>
        /// Kernel radius is calculated using the minimum viable value.
        /// See <see href="http://chemaguerra.com/gaussian-filter-radius/"/>.
        /// </summary>
        internal static int GetDefaultGaussianRadius(float sigma)
        {
            return (int)MathF.Ceiling(sigma * 3);
        }

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function.
        /// </summary>
        /// <returns>The <see cref="DenseMatrix{T}"/>.</returns>
        internal static DenseMatrix<float> CreateGaussianBlurKernel(int size, float weight)
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

        /// <summary>
        /// Create a 1 dimensional Gaussian kernel using the Gaussian G(x) function
        /// </summary>
        /// <returns>The <see cref="DenseMatrix{T}"/>.</returns>
        internal static DenseMatrix<float> CreateGaussianSharpenKernel(int size, float weight)
        {
            var kernel = new DenseMatrix<float>(size, 1);

            float sum = 0;

            float midpoint = (size - 1) / 2F;
            for (int i = 0; i < size; i++)
            {
                float x = i - midpoint;
                float gx = ImageMaths.Gaussian(x, weight);
                sum += gx;
                kernel[0, i] = gx;
            }

            // Invert the kernel for sharpening.
            int midpointRounded = (int)midpoint;
            for (int i = 0; i < size; i++)
            {
                if (i == midpointRounded)
                {
                    // Calculate central value
                    kernel[0, i] = (2F * sum) - kernel[0, i];
                }
                else
                {
                    // invert value
                    kernel[0, i] = -kernel[0, i];
                }
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
// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the kernels used for Scharr edge detection
    /// </summary>
    internal static class ScharrKernels
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public static DenseMatrix<float> ScharrX =>
            new float[,]
                {
                    { -3, 0, 3 },
                    { -10, 0, 10 },
                    { -3, 0, 3 }
                };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public static DenseMatrix<float> ScharrY =>
            new float[,]
            {
                { 3, 10, 3 },
                { 0, 0, 0 },
                { -3, -10, -3 }
            };
    }
}
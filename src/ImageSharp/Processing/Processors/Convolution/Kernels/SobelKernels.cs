// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the kernels used for Sobel edge detection
    /// </summary>
    internal static class SobelKernels
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public static DenseMatrix<float> SobelX =>
            new float[,]
                {
                    { -1, 0, 1 },
                    { -2, 0, 2 },
                    { -1, 0, 1 }
                };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public static DenseMatrix<float> SobelY =>
            new float[,]
                {
                    { -1, -2, -1 },
                    { 0, 0,  0 },
                    { 1, 2, 1 }
                };
    }
}
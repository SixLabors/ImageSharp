// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the kernels used for Kayyali edge detection
    /// </summary>
    internal static class KayyaliKernels
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public static DenseMatrix<float> KayyaliX =>
            new float[,]
                {
                    { 6, 0, -6 },
                    { 0, 0, 0 },
                    { -6, 0, 6 }
                };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public static DenseMatrix<float> KayyaliY =>
            new float[,]
                {
                    { -6, 0, 6 },
                    { 0, 0, 0 },
                    { 6, 0, -6 }
                };
    }
}

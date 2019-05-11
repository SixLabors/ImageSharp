// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the kernels used for Prewitt edge detection
    /// </summary>
    internal static class PrewittKernels
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public static DenseMatrix<float> PrewittX =>
            new float[,]
                {
                    { -1, 0, 1 },
                    { -1, 0, 1 },
                    { -1, 0, 1 }
                };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public static DenseMatrix<float> PrewittY =>
            new float[,]
                {
                    { 1, 1, 1 },
                    { 0, 0, 0 },
                    { -1, -1, -1 }
                };
    }
}
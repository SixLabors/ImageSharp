// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the kernels used for RobertsCross edge detection
    /// </summary>
    internal static class RobertsCrossKernels
    {
        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public static DenseMatrix<float> RobertsCrossX =>
            new float[,]
                {
                    { 1, 0 },
                    { 0, -1 }
                };

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public static DenseMatrix<float> RobertsCrossY =>
            new float[,]
                {
                    { 0, 1 },
                    { -1, 0 }
                };
    }
}
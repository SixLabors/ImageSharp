// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the kernels used for Robinson edge detection
    /// </summary>
    internal static class RobinsonKernels
    {
        /// <summary>
        /// Gets the North gradient operator
        /// </summary>
        public static DenseMatrix<float> RobinsonNorth =>
            new float[,]
            {
               { 1, 2, 1 },
               { 0,  0, 0 },
               { -1, -2, -1 }
            };

        /// <summary>
        /// Gets the NorthWest gradient operator
        /// </summary>
        public static DenseMatrix<float> RobinsonNorthWest =>
            new float[,]
            {
               { 2,  1, 0 },
               { 1,  0, -1 },
               { 0, -1, -2 }
            };

        /// <summary>
        /// Gets the West gradient operator
        /// </summary>
        public static DenseMatrix<float> RobinsonWest =>
            new float[,]
            {
               { 1, 0, -1 },
               { 2, 0, -2 },
               { 1, 0, -1 }
            };

        /// <summary>
        /// Gets the SouthWest gradient operator
        /// </summary>
        public static DenseMatrix<float> RobinsonSouthWest =>
            new float[,]
            {
               { 0, -1, -2 },
               { 1, 0, -1 },
               { 2, 1,  0 }
            };

        /// <summary>
        /// Gets the South gradient operator
        /// </summary>
        public static DenseMatrix<float> RobinsonSouth =>
            new float[,]
            {
               { -1, -2, -1 },
               { 0,  0, 0 },
               { 1,  2,  1 }
            };

        /// <summary>
        /// Gets the SouthEast gradient operator
        /// </summary>
        public static DenseMatrix<float> RobinsonSouthEast =>
            new float[,]
            {
               { -2, -1, 0 },
               { -1,  0, 1 },
               { 0,  1,  2 }
            };

        /// <summary>
        /// Gets the East gradient operator
        /// </summary>
        public static DenseMatrix<float> RobinsonEast =>
            new float[,]
            {
               { -1, 0, 1 },
               { -2, 0, 2 },
               { -1, 0, 1 }
            };

        /// <summary>
        /// Gets the NorthEast gradient operator
        /// </summary>
        public static DenseMatrix<float> RobinsonNorthEast =>
            new float[,]
            {
               { 0,  1,  2 },
               { -1,  0, 1 },
               { -2, -1, 0 }
            };
    }
}
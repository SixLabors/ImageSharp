// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the eight matrices used for Kirsh edge detection
    /// </summary>
    internal static class KirshKernels
    {
        /// <summary>
        /// Gets the North gradient operator
        /// </summary>
        public static DenseMatrix<float> KirschNorth =>
            new float[,]
            {
               { 5,  5,  5 },
               { -3,  0, -3 },
               { -3, -3, -3 }
            };

        /// <summary>
        /// Gets the NorthWest gradient operator
        /// </summary>
        public static DenseMatrix<float> KirschNorthWest =>
            new float[,]
            {
               { 5,  5, -3 },
               { 5,  0, -3 },
               { -3, -3, -3 }
            };

        /// <summary>
        /// Gets the West gradient operator
        /// </summary>
        public static DenseMatrix<float> KirschWest =>
            new float[,]
            {
               { 5, -3, -3 },
               { 5,  0, -3 },
               { 5, -3, -3 }
            };

        /// <summary>
        /// Gets the SouthWest gradient operator
        /// </summary>
        public static DenseMatrix<float> KirschSouthWest =>
            new float[,]
            {
               { -3, -3, -3 },
               { 5, 0, -3 },
               { 5,  5, -3 }
            };

        /// <summary>
        /// Gets the South gradient operator
        /// </summary>
        public static DenseMatrix<float> KirschSouth =>
            new float[,]
            {
               { -3, -3, -3 },
               { -3,  0, -3 },
               { 5,  5,  5 }
            };

        /// <summary>
        /// Gets the SouthEast gradient operator
        /// </summary>
        public static DenseMatrix<float> KirschSouthEast =>
            new float[,]
            {
               { -3, -3, -3 },
               { -3,  0,  5 },
               { -3,  5,  5 }
            };

        /// <summary>
        /// Gets the East gradient operator
        /// </summary>
        public static DenseMatrix<float> KirschEast =>
            new float[,]
            {
               { -3, -3, 5 },
               { -3,  0, 5 },
               { -3, -3, 5 }
            };

        /// <summary>
        /// Gets the NorthEast gradient operator
        /// </summary>
        public static DenseMatrix<float> KirschNorthEast =>
            new float[,]
            {
               { -3,  5,  5 },
               { -3,  0,  5 },
               { -3, -3, -3 }
            };
    }
}
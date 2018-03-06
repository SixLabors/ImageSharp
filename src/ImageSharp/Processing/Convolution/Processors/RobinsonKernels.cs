// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Processing.Convolution.Processors
{
    /// <summary>
    /// Contains the kernels used for Robinson edge detection
    /// </summary>
    internal static class RobinsonKernels
    {
        /// <summary>
        /// Gets the North gradient operator
        /// </summary>
        public static Fast2DArray<float> RobinsonNorth =>
            new float[,]
            {
               { 1, 2, 1 },
               { 0,  0, 0 },
               { -1, -2, -1 }
            };

        /// <summary>
        /// Gets the NorthWest gradient operator
        /// </summary>
        public static Fast2DArray<float> RobinsonNorthWest =>
            new float[,]
            {
               { 2,  1, 0 },
               { 1,  0, -1 },
               { 0, -1, -2 }
            };

        /// <summary>
        /// Gets the West gradient operator
        /// </summary>
        public static Fast2DArray<float> RobinsonWest =>
            new float[,]
            {
               { 1, 0, -1 },
               { 2, 0, -2 },
               { 1, 0, -1 }
            };

        /// <summary>
        /// Gets the SouthWest gradient operator
        /// </summary>
        public static Fast2DArray<float> RobinsonSouthWest =>
            new float[,]
            {
               { 0, -1, -2 },
               { 1, 0, -1 },
               { 2, 1,  0 }
            };

        /// <summary>
        /// Gets the South gradient operator
        /// </summary>
        public static Fast2DArray<float> RobinsonSouth =>
            new float[,]
            {
               { -1, -2, -1 },
               { 0,  0, 0 },
               { 1,  2,  1 }
            };

        /// <summary>
        /// Gets the SouthEast gradient operator
        /// </summary>
        public static Fast2DArray<float> RobinsonSouthEast =>
            new float[,]
            {
               { -2, -1, 0 },
               { -1,  0, 1 },
               { 0,  1,  2 }
            };

        /// <summary>
        /// Gets the East gradient operator
        /// </summary>
        public static Fast2DArray<float> RobinsonEast =>
            new float[,]
            {
               { -1, 0, 1 },
               { -2, 0, 2 },
               { -1, 0, 1 }
            };

        /// <summary>
        /// Gets the NorthEast gradient operator
        /// </summary>
        public static Fast2DArray<float> RobinsonNorthEast =>
            new float[,]
            {
               { 0,  1,  2 },
               { -1,  0, 1 },
               { -2, -1, 0 }
            };
    }
}
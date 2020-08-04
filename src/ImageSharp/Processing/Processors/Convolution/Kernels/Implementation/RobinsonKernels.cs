// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the kernels used for Robinson edge detection.
    /// <see href="http://www.tutorialspoint.com/dip/Robinson_Compass_Mask.htm"/>
    /// </summary>
    internal static class RobinsonKernels
    {
        /// <summary>
        /// Gets the North gradient operator
        /// </summary>
        public static DenseMatrix<float> North =>
            new float[,]
            {
               { 1, 2, 1 },
               { 0,  0, 0 },
               { -1, -2, -1 }
            };

        /// <summary>
        /// Gets the NorthWest gradient operator
        /// </summary>
        public static DenseMatrix<float> NorthWest =>
            new float[,]
            {
               { 2,  1, 0 },
               { 1,  0, -1 },
               { 0, -1, -2 }
            };

        /// <summary>
        /// Gets the West gradient operator
        /// </summary>
        public static DenseMatrix<float> West =>
            new float[,]
            {
               { 1, 0, -1 },
               { 2, 0, -2 },
               { 1, 0, -1 }
            };

        /// <summary>
        /// Gets the SouthWest gradient operator
        /// </summary>
        public static DenseMatrix<float> SouthWest =>
            new float[,]
            {
               { 0, -1, -2 },
               { 1, 0, -1 },
               { 2, 1,  0 }
            };

        /// <summary>
        /// Gets the South gradient operator
        /// </summary>
        public static DenseMatrix<float> South =>
            new float[,]
            {
               { -1, -2, -1 },
               { 0,  0, 0 },
               { 1,  2,  1 }
            };

        /// <summary>
        /// Gets the SouthEast gradient operator
        /// </summary>
        public static DenseMatrix<float> SouthEast =>
            new float[,]
            {
               { -2, -1, 0 },
               { -1,  0, 1 },
               { 0,  1,  2 }
            };

        /// <summary>
        /// Gets the East gradient operator
        /// </summary>
        public static DenseMatrix<float> East =>
            new float[,]
            {
               { -1, 0, 1 },
               { -2, 0, 2 },
               { -1, 0, 1 }
            };

        /// <summary>
        /// Gets the NorthEast gradient operator
        /// </summary>
        public static DenseMatrix<float> NorthEast =>
            new float[,]
            {
               { 0,  1,  2 },
               { -1,  0, 1 },
               { -2, -1, 0 }
            };
    }
}

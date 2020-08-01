// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the eight matrices used for Kirsch edge detection.
    /// <see href="http://en.wikipedia.org/wiki/Kirsch_operator"/>.
    /// </summary>
    internal static class KirschKernels
    {
        /// <summary>
        /// Gets the North gradient operator
        /// </summary>
        public static DenseMatrix<float> North =>
            new float[,]
            {
               { 5,  5,  5 },
               { -3,  0, -3 },
               { -3, -3, -3 }
            };

        /// <summary>
        /// Gets the NorthWest gradient operator
        /// </summary>
        public static DenseMatrix<float> NorthWest =>
            new float[,]
            {
               { 5,  5, -3 },
               { 5,  0, -3 },
               { -3, -3, -3 }
            };

        /// <summary>
        /// Gets the West gradient operator
        /// </summary>
        public static DenseMatrix<float> West =>
            new float[,]
            {
               { 5, -3, -3 },
               { 5,  0, -3 },
               { 5, -3, -3 }
            };

        /// <summary>
        /// Gets the SouthWest gradient operator
        /// </summary>
        public static DenseMatrix<float> SouthWest =>
            new float[,]
            {
               { -3, -3, -3 },
               { 5, 0, -3 },
               { 5,  5, -3 }
            };

        /// <summary>
        /// Gets the South gradient operator
        /// </summary>
        public static DenseMatrix<float> South =>
            new float[,]
            {
               { -3, -3, -3 },
               { -3,  0, -3 },
               { 5,  5,  5 }
            };

        /// <summary>
        /// Gets the SouthEast gradient operator
        /// </summary>
        public static DenseMatrix<float> SouthEast =>
            new float[,]
            {
               { -3, -3, -3 },
               { -3,  0,  5 },
               { -3,  5,  5 }
            };

        /// <summary>
        /// Gets the East gradient operator
        /// </summary>
        public static DenseMatrix<float> East =>
            new float[,]
            {
               { -3, -3, 5 },
               { -3,  0, 5 },
               { -3, -3, 5 }
            };

        /// <summary>
        /// Gets the NorthEast gradient operator
        /// </summary>
        public static DenseMatrix<float> NorthEast =>
            new float[,]
            {
               { -3,  5,  5 },
               { -3,  0,  5 },
               { -3, -3, -3 }
            };
    }
}

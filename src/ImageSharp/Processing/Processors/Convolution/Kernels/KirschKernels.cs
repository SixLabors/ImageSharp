// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains the eight matrices used for Kirsch edge detection
    /// </summary>
    internal class KirschKernels : CompassKernels
    {
        /// <summary>
        /// Gets the North gradient operator
        /// </summary>
        public override DenseMatrix<float> North =>
            new float[,]
            {
               { 5,  5,  5 },
               { -3,  0, -3 },
               { -3, -3, -3 }
            };

        /// <summary>
        /// Gets the NorthWest gradient operator
        /// </summary>
        public override DenseMatrix<float> NorthWest =>
            new float[,]
            {
               { 5,  5, -3 },
               { 5,  0, -3 },
               { -3, -3, -3 }
            };

        /// <summary>
        /// Gets the West gradient operator
        /// </summary>
        public override DenseMatrix<float> West =>
            new float[,]
            {
               { 5, -3, -3 },
               { 5,  0, -3 },
               { 5, -3, -3 }
            };

        /// <summary>
        /// Gets the SouthWest gradient operator
        /// </summary>
        public override DenseMatrix<float> SouthWest =>
            new float[,]
            {
               { -3, -3, -3 },
               { 5, 0, -3 },
               { 5,  5, -3 }
            };

        /// <summary>
        /// Gets the South gradient operator
        /// </summary>
        public override DenseMatrix<float> South =>
            new float[,]
            {
               { -3, -3, -3 },
               { -3,  0, -3 },
               { 5,  5,  5 }
            };

        /// <summary>
        /// Gets the SouthEast gradient operator
        /// </summary>
        public override DenseMatrix<float> SouthEast =>
            new float[,]
            {
               { -3, -3, -3 },
               { -3,  0,  5 },
               { -3,  5,  5 }
            };

        /// <summary>
        /// Gets the East gradient operator
        /// </summary>
        public override DenseMatrix<float> East =>
            new float[,]
            {
               { -3, -3, 5 },
               { -3,  0, 5 },
               { -3, -3, 5 }
            };

        /// <summary>
        /// Gets the NorthEast gradient operator
        /// </summary>
        public override DenseMatrix<float> NorthEast =>
            new float[,]
            {
               { -3,  5,  5 },
               { -3,  0,  5 },
               { -3, -3, -3 }
            };
    }
}
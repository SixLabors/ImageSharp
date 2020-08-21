// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Contains Laplacian kernels of different sizes.
    /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
    /// <see href="http://fourier.eng.hmc.edu/e161/lectures/gradient/node8.html"/>.
    /// </summary>
    internal static class LaplacianKernels
    {
        /// <summary>
        /// Gets the 3x3 Laplacian kernel
        /// </summary>
        public static DenseMatrix<float> Laplacian3x3 => LaplacianKernelFactory.CreateKernel(3);

        /// <summary>
        /// Gets the 5x5 Laplacian kernel
        /// </summary>
        public static DenseMatrix<float> Laplacian5x5 => LaplacianKernelFactory.CreateKernel(5);

        /// <summary>
        /// Gets the Laplacian of Gaussian kernel.
        /// </summary>
        public static DenseMatrix<float> LaplacianOfGaussianXY =>
            new float[,]
                {
                    { 0, 0, -1,  0,  0 },
                    { 0, -1, -2, -1,  0 },
                    { -1, -2, 16, -2, -1 },
                    { 0, -1, -2, -1,  0 },
                    { 0, 0, -1,  0,  0 }
                };
    }
}

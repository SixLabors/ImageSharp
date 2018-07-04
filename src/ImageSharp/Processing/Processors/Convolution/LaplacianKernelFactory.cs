// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// A factory for creating Laplacian kernel matrices.
    /// </summary>
    internal static class LaplacianKernelFactory
    {
        /// <summary>
        /// Creates a Laplacian matrix, 2nd derivative, of an arbitrary length.
        /// <see href="https://stackoverflow.com/questions/19422029/how-to-calculate-a-laplacian-mask-or-any-size"/>
        /// </summary>
        /// <param name="length">The length of the matrix sides</param>
        /// <returns>The <see cref="DenseMatrix{T}"/></returns>
        public static DenseMatrix<float> CreateKernel(uint length)
        {
            Guard.MustBeGreaterThanOrEqualTo(length, 3u, nameof(length));
            Guard.IsFalse(length % 2 == 0, nameof(length), "The kernel length must be an odd number.");

            var kernel = new DenseMatrix<float>((int)length);
            kernel.Fill(-1);

            int mid = (int)(length / 2);
            kernel[mid, mid] = (length * length) - 1;

            return kernel;
        }
    }
}
// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Represents an edge detection convolution kernel consisting of a single 2D gradient operator.
    /// </summary>
    public readonly struct EdgeDetectorKernel : IEquatable<EdgeDetectorKernel>
    {
        /// <summary>
        /// An edge detection kernel containing a 3x3 Laplacian operator.
        /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
        /// </summary>
        public static EdgeDetectorKernel Laplacian3x3 = new EdgeDetectorKernel(LaplacianKernels.Laplacian3x3);

        /// <summary>
        /// An edge detection kernel containing a 5x5 Laplacian operator.
        /// <see href="http://en.wikipedia.org/wiki/Discrete_Laplace_operator"/>
        /// </summary>
        public static EdgeDetectorKernel Laplacian5x5 = new EdgeDetectorKernel(LaplacianKernels.Laplacian5x5);

        /// <summary>
        /// An edge detection kernel containing a Laplacian of Gaussian operator.
        /// <see href="http://fourier.eng.hmc.edu/e161/lectures/gradient/node8.html"/>.
        /// </summary>
        public static EdgeDetectorKernel LaplacianOfGaussian = new EdgeDetectorKernel(LaplacianKernels.LaplacianOfGaussianXY);

        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetectorKernel"/> struct.
        /// </summary>
        /// <param name="kernelXY">The 2D gradient operator.</param>
        public EdgeDetectorKernel(DenseMatrix<float> kernelXY)
            => this.KernelXY = kernelXY;

        /// <summary>
        /// Gets the 2D gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelXY { get; }

        /// <summary>
        /// Checks whether two <see cref="EdgeDetectorKernel"/> structures are equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="EdgeDetectorKernel"/> operand.</param>
        /// <param name="right">The right hand <see cref="EdgeDetectorKernel"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        public static bool operator ==(EdgeDetectorKernel left, EdgeDetectorKernel right)
            => left.Equals(right);

        /// <summary>
        /// Checks whether two <see cref="EdgeDetectorKernel"/> structures are equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="EdgeDetectorKernel"/> operand.</param>
        /// <param name="right">The right hand <see cref="EdgeDetectorKernel"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        public static bool operator !=(EdgeDetectorKernel left, EdgeDetectorKernel right)
            => !(left == right);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is EdgeDetectorKernel kernel && this.Equals(kernel);

        /// <inheritdoc/>
        public bool Equals(EdgeDetectorKernel other)
            => this.KernelXY.Equals(other.KernelXY);

        /// <inheritdoc/>
        public override int GetHashCode() => this.KernelXY.GetHashCode();
    }
}

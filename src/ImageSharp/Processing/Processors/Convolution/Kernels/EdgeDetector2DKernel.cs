// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Processing.Processors.Convolution
{
    /// <summary>
    /// Represents an edge detection convolution kernel consisting of two 1D gradient operators.
    /// </summary>
    public readonly struct EdgeDetector2DKernel : IEquatable<EdgeDetector2DKernel>
    {
        /// <summary>
        /// An edge detection kernel containing two Kayyali operators.
        /// </summary>
        public static EdgeDetector2DKernel KayyaliKernel = new EdgeDetector2DKernel(KayyaliKernels.KayyaliX, KayyaliKernels.KayyaliY);

        /// <summary>
        /// An edge detection kernel containing two Prewitt operators.
        /// <see href="https://en.wikipedia.org/wiki/Prewitt_operator"/>.
        /// </summary>
        public static EdgeDetector2DKernel PrewittKernel = new EdgeDetector2DKernel(PrewittKernels.PrewittX, PrewittKernels.PrewittY);

        /// <summary>
        /// An edge detection kernel containing two Roberts-Cross operators.
        /// <see href="https://en.wikipedia.org/wiki/Roberts_cross"/>.
        /// </summary>
        public static EdgeDetector2DKernel RobertsCrossKernel = new EdgeDetector2DKernel(RobertsCrossKernels.RobertsCrossX, RobertsCrossKernels.RobertsCrossY);

        /// <summary>
        /// An edge detection kernel containing two Scharr operators.
        /// </summary>
        public static EdgeDetector2DKernel ScharrKernel = new EdgeDetector2DKernel(ScharrKernels.ScharrX, ScharrKernels.ScharrY);

        /// <summary>
        /// An edge detection kernel containing two Sobel operators.
        /// <see href="https://en.wikipedia.org/wiki/Sobel_operator"/>.
        /// </summary>
        public static EdgeDetector2DKernel SobelKernel = new EdgeDetector2DKernel(SobelKernels.SobelX, SobelKernels.SobelY);

        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeDetector2DKernel"/> struct.
        /// </summary>
        /// <param name="kernelX">The horizontal gradient operator.</param>
        /// <param name="kernelY">The vertical gradient operator.</param>
        public EdgeDetector2DKernel(DenseMatrix<float> kernelX, DenseMatrix<float> kernelY)
        {
            Guard.IsTrue(
                kernelX.Size.Equals(kernelY.Size),
                $"{nameof(kernelX)} {nameof(kernelY)}",
                "Kernel sizes must be the same.");

            this.KernelX = kernelX;
            this.KernelY = kernelY;
        }

        /// <summary>
        /// Gets the horizontal gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelX { get; }

        /// <summary>
        /// Gets the vertical gradient operator.
        /// </summary>
        public DenseMatrix<float> KernelY { get; }

        /// <summary>
        /// Checks whether two <see cref="EdgeDetector2DKernel"/> structures are equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="EdgeDetector2DKernel"/> operand.</param>
        /// <param name="right">The right hand <see cref="EdgeDetector2DKernel"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        public static bool operator ==(EdgeDetector2DKernel left, EdgeDetector2DKernel right)
            => left.Equals(right);

        /// <summary>
        /// Checks whether two <see cref="EdgeDetector2DKernel"/> structures are equal.
        /// </summary>
        /// <param name="left">The left hand <see cref="EdgeDetector2DKernel"/> operand.</param>
        /// <param name="right">The right hand <see cref="EdgeDetector2DKernel"/> operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter;
        /// otherwise, false.
        /// </returns>
        public static bool operator !=(EdgeDetector2DKernel left, EdgeDetector2DKernel right)
            => !(left == right);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is EdgeDetector2DKernel kernel && this.Equals(kernel);

        /// <inheritdoc/>
        public bool Equals(EdgeDetector2DKernel other)
            => this.KernelX.Equals(other.KernelX)
            && this.KernelY.Equals(other.KernelY);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.KernelX, this.KernelY);
    }
}

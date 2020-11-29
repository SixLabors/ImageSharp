// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// A vector with 4 values of type <see cref="Complex64"/>.
    /// </summary>
    internal struct ComplexVector4 : IEquatable<ComplexVector4>
    {
        /// <summary>
        /// The real part of the complex vector
        /// </summary>
        public Vector4 Real;

        /// <summary>
        /// The imaginary part of the complex number
        /// </summary>
        public Vector4 Imaginary;

        /// <summary>
        /// Sums the values in the input <see cref="ComplexVector4"/> to the current instance
        /// </summary>
        /// <param name="value">The input <see cref="ComplexVector4"/> to sum</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void Sum(ComplexVector4 value)
        {
            this.Real += value.Real;
            this.Imaginary += value.Imaginary;
        }

        /// <summary>
        /// Performs a weighted sum on the current instance according to the given parameters
        /// </summary>
        /// <param name="a">The 'a' parameter, for the real component</param>
        /// <param name="b">The 'b' parameter, for the imaginary component</param>
        /// <returns>The resulting <see cref="Vector4"/> value</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector4 WeightedSum(float a, float b) => (this.Real * a) + (this.Imaginary * b);

        /// <inheritdoc/>
        public bool Equals(ComplexVector4 other)
        {
            return this.Real.Equals(other.Real) && this.Imaginary.Equals(other.Imaginary);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ComplexVector4 other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Real.GetHashCode() * 397) ^ this.Imaginary.GetHashCode();
            }
        }
    }
}

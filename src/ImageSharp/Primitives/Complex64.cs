// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a complex number, where the real and imaginary parts are stored as <see cref="float"/> values.
    /// </summary>
    /// <remarks>
    /// This is a more efficient version of the <see cref="Complex64"/> type.
    /// </remarks>
    internal readonly struct Complex64 : IEquatable<Complex64>
    {
        /// <summary>
        /// The real part of the complex number
        /// </summary>
        public readonly float Real;

        /// <summary>
        /// The imaginary part of the complex number
        /// </summary>
        public readonly float Imaginary;

        /// <summary>
        /// Initializes a new instance of the <see cref="Complex64"/> struct.
        /// </summary>
        /// <param name="real">The real part in the complex number.</param>
        /// <param name="imaginary">The imaginary part in the complex number.</param>
        public Complex64(float real, float imaginary)
        {
            this.Real = real;
            this.Imaginary = imaginary;
        }

        /// <summary>
        /// Performs the multiplication operation between a <see cref="Complex64"/> instance and a <see cref="float"/> scalar.
        /// </summary>
        /// <param name="value">The <see cref="Complex64"/> value to multiply.</param>
        /// <param name="scalar">The <see cref="float"/> scalar to use to multiply the <see cref="Complex64"/> value.</param>
        /// <returns>The <see cref="Complex64"/> result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static Complex64 operator *(Complex64 value, float scalar) => new Complex64(value.Real * scalar, value.Imaginary * scalar);

        /// <summary>
        /// Performs the multiplication operation between a <see cref="Complex64"/> instance and a <see cref="Vector4"/>.
        /// </summary>
        /// <param name="value">The <see cref="Complex64"/> value to multiply.</param>
        /// <param name="vector">The <see cref="Vector4"/> instance to use to multiply the <see cref="Complex64"/> value.</param>
        /// <returns>The <see cref="Complex64"/> result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static ComplexVector4 operator *(Complex64 value, Vector4 vector)
        {
            return new ComplexVector4 { Real = vector * value.Real, Imaginary = vector * value.Imaginary };
        }

        /// <summary>
        /// Performs the multiplication operation between a <see cref="Complex64"/> instance and a <see cref="ComplexVector4"/>.
        /// </summary>
        /// <param name="value">The <see cref="Complex64"/> value to multiply.</param>
        /// <param name="vector">The <see cref="ComplexVector4"/> instance to use to multiply the <see cref="Complex64"/> value.</param>
        /// <returns>The <see cref="Complex64"/> result</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static ComplexVector4 operator *(Complex64 value, ComplexVector4 vector)
        {
            Vector4 real = (value.Real * vector.Real) - (value.Imaginary * vector.Imaginary);
            Vector4 imaginary = (value.Real * vector.Imaginary) + (value.Imaginary * vector.Real);
            return new ComplexVector4 { Real = real, Imaginary = imaginary };
        }

        /// <inheritdoc/>
        public bool Equals(Complex64 other)
        {
            return this.Real.Equals(other.Real) && this.Imaginary.Equals(other.Imaginary);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Complex64 other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (this.Real.GetHashCode() * 397) ^ this.Imaginary.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => $"{this.Real}{(this.Imaginary >= 0 ? "+" : string.Empty)}{this.Imaginary}j";
    }
}

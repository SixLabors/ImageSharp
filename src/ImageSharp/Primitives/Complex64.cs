// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Primitives
{
    /// <summary>
    /// Represents a complex number, where the real and imaginary parts are stored as <see cref="float"/> values.
    /// </summary>
    /// <remarks>
    /// This is a more efficient version of the <see cref="System.Numerics.Complex"/> type.
    /// </remarks>
    internal readonly struct Complex64
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
        /// Performs the multiplication operation between a <see cref="Complex64"/> intance and a <see cref="float"/> scalar.
        /// </summary>
        /// <param name="value">The <see cref="Complex64"/> value to multiply.</param>
        /// <param name="scalar">The <see cref="float"/> scalar to use to multiply the <see cref="Complex64"/> value.</param>
        /// <returns>The <see cref="Complex64"/> result</returns>
        public static Complex64 operator *(Complex64 value, float scalar) => new Complex64(value.Real * scalar, value.Imaginary * scalar);

        /// <summary>
        /// Performs the addition operation between two <see cref="Complex64"/> intances.
        /// </summary>
        /// <param name="left">The first <see cref="Complex64"/> value to sum.</param>
        /// <param name="right">The second <see cref="Complex64"/> value to sum.</param>
        /// <returns>The <see cref="Complex64"/> result</returns>
        public static Complex64 operator +(Complex64 left, Complex64 right) => new Complex64(left.Real + right.Real, left.Imaginary + right.Imaginary);
    }
}

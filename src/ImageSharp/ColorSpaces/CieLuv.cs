// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// The CIE 1976 (L*, u*, v*) color space, commonly known by its abbreviation CIELUV, is a color space adopted by the International
    /// Commission on Illumination (CIE) in 1976, as a simple-to-compute transformation of the 1931 CIE XYZ color space, but which
    /// attempted perceptual uniformity
    /// <see href="https://en.wikipedia.org/wiki/CIELUV"/>
    /// </summary>
    internal readonly struct CieLuv : IEquatable<CieLuv>
    {
        /// <summary>
        /// D65 standard illuminant.
        /// Used when reference white is not specified explicitly.
        /// </summary>
        public static readonly CieXyz DefaultWhitePoint = Illuminants.D65;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, u, v components.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv(Vector3 vector)
            : this(vector.X, vector.Y, vector.Z, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, u, v components.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv(Vector3 vector, CieXyz whitePoint)
            : this(vector.X, vector.Y, vector.Z, whitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="u">The blue-yellow chromaticity coordinate of the given whitepoint.</param>
        /// <param name="v">The red-green chromaticity coordinate of the given whitepoint.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv(float l, float u, float v)
            : this(l, u, v, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="u">The blue-yellow chromaticity coordinate of the given whitepoint.</param>
        /// <param name="v">The red-green chromaticity coordinate of the given whitepoint.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv(float l, float u, float v, CieXyz whitePoint)
        {
            this.L = l;
            this.U = u;
            this.V = v;
            this.WhitePoint = whitePoint;
        }

        /// <summary>
        /// Gets the reference white point of this color.
        /// </summary>
        public CieXyz WhitePoint { get; }

        /// <summary>
        /// Gets the lightness dimension
        /// <remarks>A value usually ranging between 0 and 100.</remarks>
        /// </summary>
        public float L { get; }

        /// <summary>
        /// Gets the blue-yellow chromaticity coordinate of the given whitepoint.
        /// <remarks>A value usually ranging between -100 and 100.</remarks>
        /// </summary>
        public float U { get; }

        /// <summary>
        /// Gets the red-green chromaticity coordinate of the given whitepoint.
        /// <remarks>A value usually ranging between -100 and 100.</remarks>
        /// </summary>
        public float V { get; }

        /// <summary>
        /// Compares two <see cref="CieLuv"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieLuv"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieLuv"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CieLuv left, CieLuv right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieLuv"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieLuv"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieLuv"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CieLuv left, CieLuv right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashHelpers.Combine(this.WhitePoint.GetHashCode(), (this.L, this.U, this.V).GetHashCode());
        }

        /// <inheritdoc/>
        public override string ToString() => $"CieLuv({this.L:#0.##},{this.U:#0.##},{this.V:#0.##})";

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CieLuv other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CieLuv other) =>
            this.L == other.L &&
            this.U == other.U &&
            this.V == other.V &&
            this.WhitePoint.Equals(other.WhitePoint);
    }
}
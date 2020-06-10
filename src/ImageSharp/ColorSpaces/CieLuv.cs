// Copyright (c) Six Labors.
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
    public readonly struct CieLuv : IEquatable<CieLuv>
    {
        /// <summary>
        /// D65 standard illuminant.
        /// Used when reference white is not specified explicitly.
        /// </summary>
        public static readonly CieXyz DefaultWhitePoint = Illuminants.D65;

        /// <summary>
        /// Gets the lightness dimension
        /// <remarks>A value usually ranging between 0 and 100.</remarks>
        /// </summary>
        public readonly float L;

        /// <summary>
        /// Gets the blue-yellow chromaticity coordinate of the given whitepoint.
        /// <remarks>A value usually ranging between -100 and 100.</remarks>
        /// </summary>
        public readonly float U;

        /// <summary>
        /// Gets the red-green chromaticity coordinate of the given whitepoint.
        /// <remarks>A value usually ranging between -100 and 100.</remarks>
        /// </summary>
        public readonly float V;

        /// <summary>
        /// Gets the reference white point of this color
        /// </summary>
        public readonly CieXyz WhitePoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="u">The blue-yellow chromaticity coordinate of the given whitepoint.</param>
        /// <param name="v">The red-green chromaticity coordinate of the given whitepoint.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(InliningOptions.ShortMethod)]
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
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLuv(float l, float u, float v, CieXyz whitePoint)
            : this(new Vector3(l, u, v), whitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, u, v components.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLuv(Vector3 vector)
            : this(vector, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, u, v components.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLuv(Vector3 vector, CieXyz whitePoint)
        {
            // Not clamping as documentation about this space only indicates "usual" ranges
            this.L = vector.X;
            this.U = vector.Y;
            this.V = vector.Z;
            this.WhitePoint = whitePoint;
        }

        /// <summary>
        /// Compares two <see cref="CieLuv"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="CieLuv"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieLuv"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(CieLuv left, CieLuv right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="CieLuv"/> objects for inequality.
        /// </summary>
        /// <param name="left">The <see cref="CieLuv"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieLuv"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(CieLuv left, CieLuv right) => !left.Equals(right);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.L, this.U, this.V, this.WhitePoint);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"CieLuv({this.L:#0.##}, {this.U:#0.##}, {this.V:#0.##})");

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is CieLuv other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(CieLuv other)
        {
            return this.L.Equals(other.L)
                && this.U.Equals(other.U)
                && this.V.Equals(other.V)
                && this.WhitePoint.Equals(other.WhitePoint);
        }
    }
}
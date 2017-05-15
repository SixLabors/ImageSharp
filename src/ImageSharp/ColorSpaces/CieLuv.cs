// <copyright file="CieLuv.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces
{
    using System;
    using System.ComponentModel;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// The CIE 1976 (L*, u*, v*) color space, commonly known by its abbreviation CIELUV, is a color space adopted by the International
    /// Commission on Illumination (CIE) in 1976, as a simple-to-compute transformation of the 1931 CIE XYZ color space, but which
    /// attempted perceptual uniformity
    /// <see href="https://en.wikipedia.org/wiki/CIELUV"/>
    /// </summary>
    internal struct CieLuv : IColorVector, IEquatable<CieLuv>, IAlmostEquatable<CieLuv, float>
    {
        /// <summary>
        /// D65 standard illuminant.
        /// Used when reference white is not specified explicitly.
        /// </summary>
        public static readonly CieXyz DefaultWhitePoint = Illuminants.D65;

        /// <summary>
        /// Represents a <see cref="CieLuv"/> that has L, U, and V values set to zero.
        /// </summary>
        public static readonly CieLuv Empty = default(CieLuv);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="u">The blue-yellow chromaticity coordinate of the given whitepoint.</param>
        /// <param name="v">The red-green chromaticity coordinate of the given whitepoint.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv(float l, float u, float v)
            : this(new Vector3(l, u, v), DefaultWhitePoint)
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
            : this(new Vector3(l, u, v), whitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, u, v components.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv(Vector3 vector)
            : this(vector, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, u, v components.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLuv(Vector3 vector, CieXyz whitePoint)
            : this()
        {
            this.backingVector = vector;
            this.WhitePoint = whitePoint;
        }

        /// <summary>
        /// Gets the reference white point of this color
        /// </summary>
        public CieXyz WhitePoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets the lightness dimension
        /// <remarks>A value usually ranging between 0 and 100.</remarks>
        /// </summary>
        public float L
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the blue-yellow chromaticity coordinate of the given whitepoint.
        /// <remarks>A value usually ranging between -100 and 100.</remarks>
        /// </summary>
        public float U
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

        /// <summary>
        /// Gets the red-green chromaticity coordinate of the given whitepoint.
        /// <remarks>A value usually ranging between -100 and 100.</remarks>
        /// </summary>
        public float V
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="CieLuv"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <inheritdoc />
        public Vector3 Vector
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector;
        }

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
            unchecked
            {
                int hashCode = this.WhitePoint.GetHashCode();
                hashCode = (hashCode * 397) ^ this.backingVector.GetHashCode();
                return hashCode;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "CieLuv [ Empty ]";
            }

            return $"CieLuv [ L={this.L:#0.##}, U={this.U:#0.##}, V={this.V:#0.##} ]";
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is CieLuv)
            {
                return this.Equals((CieLuv)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CieLuv other)
        {
            return this.backingVector.Equals(other.backingVector)
                   && this.WhitePoint.Equals(other.WhitePoint);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(CieLuv other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return this.WhitePoint.Equals(other.WhitePoint)
                   && result.X <= precision
                   && result.Y <= precision
                   && result.Z <= precision;
        }
    }
}
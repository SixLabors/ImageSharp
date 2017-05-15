// <copyright file="CieLchuv.cs" company="James Jackson-South">
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
    /// Represents the CIE L*C*h°, cylindrical form of the CIE L*u*v* 1976 color.
    /// <see href="https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CieLchuv_or_CIEHLC"/>
    /// </summary>
    internal struct CieLchuv : IColorVector, IEquatable<CieLchuv>, IAlmostEquatable<CieLchuv, float>
    {
        /// <summary>
        /// D50 standard illuminant.
        /// Used when reference white is not specified explicitly.
        /// </summary>
        public static readonly CieXyz DefaultWhitePoint = Illuminants.D65;

        /// <summary>
        /// Represents a <see cref="CieLchuv"/> that has L, C, H values set to zero.
        /// </summary>
        public static readonly CieLchuv Empty = default(CieLchuv);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="c">The chroma, relative saturation.</param>
        /// <param name="h">The hue in degrees.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLchuv(float l, float c, float h)
            : this(new Vector3(l, c, h), DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="c">The chroma, relative saturation.</param>
        /// <param name="h">The hue in degrees.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLchuv(float l, float c, float h, CieXyz whitePoint)
            : this(new Vector3(l, c, h), whitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, c, h components.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLchuv(Vector3 vector)
            : this(vector, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, c, h components.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLchuv(Vector3 vector, CieXyz whitePoint)
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
        /// Gets the lightness dimension.
        /// <remarks>A value ranging between 0 (black), 100 (diffuse white) or higher (specular white).</remarks>
        /// </summary>
        public float L
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the a chroma component.
        /// <remarks>A value ranging from 0 to 100.</remarks>
        /// </summary>
        public float C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

        /// <summary>
        /// Gets the h° hue component in degrees.
        /// <remarks>A value ranging from 0 to 360.</remarks>
        /// </summary>
        public float H
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="CieLchuv"/> is empty.
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
        /// Compares two <see cref="CieLchuv"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieLchuv"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieLchuv"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CieLchuv left, CieLchuv right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieLchuv"/> objects for inequality
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieLchuv"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieLchuv"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CieLchuv left, CieLchuv right)
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
                return "CieLchuv [Empty]";
            }

            return $"CieLchuv [ L={this.L:#0.##}, C={this.C:#0.##}, H={this.H:#0.##}]";
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is CieLchuv)
            {
                return this.Equals((CieLchuv)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CieLchuv other)
        {
            return this.backingVector.Equals(other.backingVector)
                && this.WhitePoint.Equals(other.WhitePoint);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(CieLchuv other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return this.WhitePoint.Equals(other.WhitePoint)
                   && result.X <= precision
                   && result.Y <= precision
                   && result.Z <= precision;
        }

        /// <summary>
        /// Computes the saturation of the color (chroma normalized by lightness)
        /// </summary>
        /// <remarks>
        /// A value ranging from 0 to 100.
        /// </remarks>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Saturation()
        {
            float result = 100 * (this.C / this.L);

            if (float.IsNaN(result))
            {
                return 0;
            }

            return result;
        }
    }
}
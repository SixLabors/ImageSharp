﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents the CIE L*C*h°, cylindrical form of the CIE L*a*b* 1976 color.
    /// <see href="https://en.wikipedia.org/wiki/Lab_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC"/>
    /// </summary>
    internal readonly struct CieLch : IEquatable<CieLch>
    {
        /// <summary>
        /// D50 standard illuminant.
        /// Used when reference white is not specified explicitly.
        /// </summary>
        public static readonly CieXyz DefaultWhitePoint = Illuminants.D50;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLch"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, c, h components.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLch(Vector3 vector)
            : this(vector.X, vector.Y, vector.Z, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLch"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, c, h components.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLch(Vector3 vector, CieXyz whitePoint)
            : this(vector.X, vector.Y, vector.Z, whitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLch"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="c">The chroma, relative saturation.</param>
        /// <param name="h">The hue in degrees.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieLch(float l, float c, float h)
            : this(new Vector3(l, c, h), DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLch"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="c">The chroma, relative saturation.</param>
        /// <param name="h">The hue in degrees.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        public CieLch(float l, float c, float h, CieXyz whitePoint)
        {
            this.L = l;
            this.C = c;
            this.H = h;
            this.WhitePoint = whitePoint;
        }

        /// <summary>
        /// Gets the reference white point of this color.
        /// </summary>
        public CieXyz WhitePoint { get; }

        /// <summary>
        /// Gets the lightness dimension.
        /// <remarks>A value ranging between 0 (black), 100 (diffuse white) or higher (specular white).</remarks>
        /// </summary>
        public float L { get; }

        /// <summary>
        /// Gets the a chroma component.
        /// <remarks>A value ranging from 0 to 100.</remarks>
        /// </summary>
        public float C { get; }

        /// <summary>
        /// Gets the h° hue component in degrees.
        /// <remarks>A value ranging from 0 to 360.</remarks>
        /// </summary>
        public float H { get; }

        /// <summary>
        /// Compares two <see cref="CieLch"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="CieLch"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieLch"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CieLch left, CieLch right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieLch"/> objects for inequality
        /// </summary>
        /// <param name="left">The <see cref="CieLch"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieLch"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CieLch left, CieLch right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashHelpers.Combine(this.WhitePoint.GetHashCode(), (this.L, this.C, this.H).GetHashCode());
        }

        /// <inheritdoc/>
        public override string ToString() => $"CieLch({this.L:#0.##},{this.C:#0.##},{this.H:#0.##})";

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is CieLch other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CieLch other) =>
            this.L == other.L &&
            this.C == other.C &&
            this.H == other.H &&
            this.WhitePoint.Equals(other.WhitePoint);

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
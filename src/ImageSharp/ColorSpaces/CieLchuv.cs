// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents the CIE L*C*h°, cylindrical form of the CIE L*u*v* 1976 color.
    /// <see href="https://en.wikipedia.org/wiki/CIELAB_color_space#Cylindrical_representation:_CIELCh_or_CIEHLC"/>
    /// </summary>
    public readonly struct CieLchuv : IEquatable<CieLchuv>
    {
        private static readonly Vector3 Min = new Vector3(0, -200, 0);
        private static readonly Vector3 Max = new Vector3(100, 200, 360);

        /// <summary>
        /// D50 standard illuminant.
        /// Used when reference white is not specified explicitly.
        /// </summary>
        public static readonly CieXyz DefaultWhitePoint = Illuminants.D65;

        /// <summary>
        /// Gets the lightness dimension.
        /// <remarks>A value ranging between 0 (black), 100 (diffuse white) or higher (specular white).</remarks>
        /// </summary>
        public readonly float L;

        /// <summary>
        /// Gets the a chroma component.
        /// <remarks>A value ranging from 0 to 200.</remarks>
        /// </summary>
        public readonly float C;

        /// <summary>
        /// Gets the h° hue component in degrees.
        /// <remarks>A value ranging from 0 to 360.</remarks>
        /// </summary>
        public readonly float H;

        /// <summary>
        /// Gets the reference white point of this color
        /// </summary>
        public readonly CieXyz WhitePoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="c">The chroma, relative saturation.</param>
        /// <param name="h">The hue in degrees.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLchuv(float l, float c, float h)
            : this(l, c, h, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="c">The chroma, relative saturation.</param>
        /// <param name="h">The hue in degrees.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLchuv(float l, float c, float h, CieXyz whitePoint)
           : this(new Vector3(l, c, h), whitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, c, h components.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLchuv(Vector3 vector)
            : this(vector, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLchuv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, c, h components.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLchuv(Vector3 vector, CieXyz whitePoint)
            : this()
        {
            vector = Vector3.Clamp(vector, Min, Max);
            this.L = vector.X;
            this.C = vector.Y;
            this.H = vector.Z;
            this.WhitePoint = whitePoint;
        }

        /// <summary>
        /// Compares two <see cref="CieLchuv"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="CieLchuv"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieLchuv"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(CieLchuv left, CieLchuv right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="CieLchuv"/> objects for inequality
        /// </summary>
        /// <param name="left">The <see cref="CieLchuv"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieLchuv"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(CieLchuv left, CieLchuv right) => !left.Equals(right);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.L, this.C, this.H, this.WhitePoint);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"CieLchuv({this.L:#0.##}, {this.C:#0.##}, {this.H:#0.##})");

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is CieLchuv other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(CieLchuv other)
        {
            return this.L.Equals(other.L)
                && this.C.Equals(other.C)
                && this.H.Equals(other.H)
                && this.WhitePoint.Equals(other.WhitePoint);
        }

        /// <summary>
        /// Computes the saturation of the color (chroma normalized by lightness)
        /// </summary>
        /// <remarks>
        /// A value ranging from 0 to 100.
        /// </remarks>
        /// <returns>The <see cref="float"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
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
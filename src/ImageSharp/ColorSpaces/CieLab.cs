// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents a CIE L*a*b* 1976 color.
    /// <see href="https://en.wikipedia.org/wiki/Lab_color_space"/>
    /// </summary>
    public readonly struct CieLab : IEquatable<CieLab>
    {
        /// <summary>
        /// D50 standard illuminant.
        /// Used when reference white is not specified explicitly.
        /// </summary>
        public static readonly CieXyz DefaultWhitePoint = Illuminants.D50;

        /// <summary>
        /// Gets the lightness dimension.
        /// <remarks>A value usually ranging between 0 (black), 100 (diffuse white) or higher (specular white).</remarks>
        /// </summary>
        public readonly float L;

        /// <summary>
        /// Gets the a color component.
        /// <remarks>A value usually ranging from -100 to 100. Negative is green, positive magenta.</remarks>
        /// </summary>
        public readonly float A;

        /// <summary>
        /// Gets the b color component.
        /// <remarks>A value usually ranging from -100 to 100. Negative is blue, positive is yellow</remarks>
        /// </summary>
        public readonly float B;

        /// <summary>
        /// Gets the reference white point of this color
        /// </summary>
        public readonly CieXyz WhitePoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLab"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="a">The a (green - magenta) component.</param>
        /// <param name="b">The b (blue - yellow) component.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLab(float l, float a, float b)
            : this(l, a, b, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLab"/> struct.
        /// </summary>
        /// <param name="l">The lightness dimension.</param>
        /// <param name="a">The a (green - magenta) component.</param>
        /// <param name="b">The b (blue - yellow) component.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLab(float l, float a, float b, CieXyz whitePoint)
            : this(new Vector3(l, a, b), whitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLab"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, a, b components.</param>
        /// <remarks>Uses <see cref="DefaultWhitePoint"/> as white point.</remarks>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLab(Vector3 vector)
            : this(vector, DefaultWhitePoint)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieLab"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, a, b components.</param>
        /// <param name="whitePoint">The reference white point. <see cref="Illuminants"/></param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public CieLab(Vector3 vector, CieXyz whitePoint)
            : this()
        {
            // Not clamping as documentation about this space only indicates "usual" ranges
            this.L = vector.X;
            this.A = vector.Y;
            this.B = vector.Z;
            this.WhitePoint = whitePoint;
        }

        /// <summary>
        /// Compares two <see cref="CieLab"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="CieLab"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieLab"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(CieLab left, CieLab right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="CieLab"/> objects for inequality
        /// </summary>
        /// <param name="left">The <see cref="CieLab"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="CieLab"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(CieLab left, CieLab right) => !left.Equals(right);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.L, this.A, this.B, this.WhitePoint);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"CieLab({this.L:#0.##}, {this.A:#0.##}, {this.B:#0.##})");

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is CieLab other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(CieLab other)
        {
            return this.L.Equals(other.L)
                && this.A.Equals(other.A)
                && this.B.Equals(other.B)
                && this.WhitePoint.Equals(other.WhitePoint);
        }
    }
}
// <copyright file="CieXyz.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents an CIE 1931 color
    /// <see href="https://en.wikipedia.org/wiki/CIE_1931_color_space"/>
    /// </summary>
    public struct CieXyz : IEquatable<CieXyz>
    {
        /// <summary>
        /// Represents a <see cref="CieXyz"/> that has Y, Cb, and Cr values set to zero.
        /// </summary>
        public static readonly CieXyz Empty = default(CieXyz);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyz"/> struct.
        /// </summary>
        /// <param name="y">The y luminance component.</param>
        /// <param name="x">X is a mix (a linear combination) of cone response curves chosen to be nonnegative</param>
        /// <param name="z">Z is quasi-equal to blue stimulation, or the S cone of the human eye.</param>
        public CieXyz(float x, float y, float z)
            : this()
        {
            // ToDo: check clamp values
            this.backingVector.X = x.Clamp(0, 2);
            this.backingVector.Y = y.Clamp(0, 2);
            this.backingVector.Z = z.Clamp(0, 2);
        }

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value ranging between 380 and 780.</remarks>
        /// </summary>
        public float X => this.backingVector.X;

        /// <summary>
        /// Gets the Cb chroma component.
        /// <remarks>A value ranging between 380 and 780.</remarks>
        /// </summary>
        public float Y => this.backingVector.Y;

        /// <summary>
        /// Gets the Cr chroma component.
        /// <remarks>A value ranging between 380 and 780.</remarks>
        /// </summary>
        public float Z => this.backingVector.Z;

        /// <summary>
        /// Gets a value indicating whether this <see cref="CieXyz"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.backingVector.Equals(default(Vector3));

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Color"/> to a
        /// <see cref="CieXyz"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="CieXyz"/>.
        /// </returns>
        public static implicit operator CieXyz(Color color)
        {
            var r = color.R;
            var g = color.G;
            var b = color.B;

            // assume sRGB
            r = r > 0.04045 ? (float)Math.Pow(((r + 0.055) / 1.055), 2.4) : (float)(r / 12.92);
            g = g > 0.04045 ? (float)Math.Pow(((g + 0.055) / 1.055), 2.4) : (float)(g / 12.92);
            b = b > 0.04045 ? (float)Math.Pow(((b + 0.055) / 1.055), 2.4) : (float)(b / 12.92);

            var x = (r * 0.41239079926595F) + (g * 0.35758433938387F) + (b * 0.18048078840183F);
            var y = (r * 0.21263900587151F) + (g * 0.71516867876775F) + (b * 0.072192315360733F);
            var z = (r * 0.019330818715591F) + (g * 0.11919477979462F) + (b * 0.95053215224966F);

            return new CieXyz(x, y, z);
        }

        /// <summary>
        /// Compares two <see cref="CieXyz"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieXyz"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieXyz"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(CieXyz left, CieXyz right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieXyz"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieXyz"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieXyz"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(CieXyz left, CieXyz right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is CieXyz)
            {
                CieXyz color = (CieXyz)obj;

                return this.backingVector == color.backingVector;
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "CieXyz [ Empty ]";
            }

            return $"CieXyz [ X={this.X:#0.##}, Y={this.Y:#0.##}, Z={this.Z:#0.##} ]";
        }

        /// <inheritdoc/>
        public bool Equals(CieXyz other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Hsv"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private static int GetHashCode(CieXyz color) => color.backingVector.GetHashCode();
    }
}

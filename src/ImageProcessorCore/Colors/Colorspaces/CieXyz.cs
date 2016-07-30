// <copyright file="CieXyz.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents an CIE 1931 color
    /// <see href="https://en.wikipedia.org/wiki/CIE_1931_color_space"/>
    /// </summary>
    public struct CieXyz : IEquatable<CieXyz>, IAlmostEquatable<CieXyz, float>
    {
        /// <summary>
        /// Represents a <see cref="CieXyz"/> that has Y, Cb, and Cr values set to zero.
        /// </summary>
        public static readonly CieXyz Empty = default(CieXyz);

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.001f;

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
            // Not clamping as documentation about this space seems to indicate "usual" ranges
            this.backingVector = new Vector3(x, y, z);
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
        public bool IsEmpty => this.Equals(Empty);

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
            Vector4 vector = color.ToVector4().Expand();

            float x = (vector.X * 0.4124F) + (vector.Y * 0.3576F) + (vector.Z * 0.1805F);
            float y = (vector.X * 0.2126F) + (vector.Y * 0.7152F) + (vector.Z * 0.0722F);
            float z = (vector.X * 0.0193F) + (vector.Y * 0.1192F) + (vector.Z * 0.9505F);

            x *= 100F;
            y *= 100F;
            z *= 100F;

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
        public override bool Equals(object obj)
        {
            if (obj is CieXyz)
            {
                return this.Equals((CieXyz)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(CieXyz other)
        {
            return this.AlmostEquals(other, Epsilon);
        }

        /// <inheritdoc/>
        public bool AlmostEquals(CieXyz other, float precision)
        {
            Vector3 result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X < precision
                && result.Y < precision
                && result.Z < precision;
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

// <copyright file="YCbCr.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents an YCbCr (luminance, blue chroma, red chroma) color conforming to the
    /// Full range standard used in digital imaging systems.
    /// <see href="http://en.wikipedia.org/wiki/YCbCr"/>
    /// </summary>
    public struct YCbCr : IEquatable<YCbCr>, IAlmostEquatable<YCbCr, float>
    {
        /// <summary>
        /// Represents a <see cref="YCbCr"/> that has Y, Cb, and Cr values set to zero.
        /// </summary>
        public static readonly YCbCr Empty = default(YCbCr);

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.001F;

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCr"/> struct.
        /// </summary>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        public YCbCr(float y, float cb, float cr)
            : this()
        {
            this.backingVector = Vector3.Clamp(new Vector3(y, cb, cr), Vector3.Zero, new Vector3(255));
        }

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Y => this.backingVector.X;

        /// <summary>
        /// Gets the Cb chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Cb => this.backingVector.Y;

        /// <summary>
        /// Gets the Cr chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Cr => this.backingVector.Z;

        /// <summary>
        /// Gets a value indicating whether this <see cref="YCbCr"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Color"/> to a
        /// <see cref="YCbCr"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Color"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="YCbCr"/>.
        /// </returns>
        public static implicit operator YCbCr(Color color)
        {
            float r = color.R;
            float g = color.G;
            float b = color.B;

            float y = (float)((0.299 * r) + (0.587 * g) + (0.114 * b));
            float cb = 128 + (float)((-0.168736 * r) - (0.331264 * g) + (0.5 * b));
            float cr = 128 + (float)((0.5 * r) - (0.418688 * g) - (0.081312 * b));

            return new YCbCr(y, cb, cr);
        }

        /// <summary>
        /// Compares two <see cref="YCbCr"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="YCbCr"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="YCbCr"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(YCbCr left, YCbCr right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="YCbCr"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="YCbCr"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="YCbCr"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(YCbCr left, YCbCr right)
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
                return "YCbCr [ Empty ]";
            }

            return $"YCbCr [ Y={this.Y:#0.##}, Cb={this.Cb:#0.##}, Cr={this.Cr:#0.##} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is YCbCr)
            {
                return this.Equals((YCbCr)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(YCbCr other)
        {
            return this.AlmostEquals(other, Epsilon);
        }

        /// <inheritdoc/>
        public bool AlmostEquals(YCbCr other, float precision)
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
        /// The instance of <see cref="YCbCr"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private static int GetHashCode(YCbCr color) => color.backingVector.GetHashCode();
    }
}

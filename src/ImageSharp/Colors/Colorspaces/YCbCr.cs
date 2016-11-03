// <copyright file="YCbCr.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Represents an YCbCr (luminance, blue chroma, red chroma) color conforming to the full range standard used in digital imaging systems.
    /// <see href="http://en.wikipedia.org/wiki/YCbCr"/>
    /// </summary>
    public struct YCbCr : IEquatable<YCbCr>
    {
        /// <summary>
        /// Represents a <see cref="YCbCr"/> that has Y, Cb, and Cr values set to zero.
        /// </summary>
        public static readonly YCbCr Empty = default(YCbCr);

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCr"/> struct.
        /// </summary>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        public YCbCr(byte y, byte cb, byte cr)
            : this()
        {
            this.Y = y;
            this.Cb = cb;
            this.Cr = cr;
        }

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public byte Y { get; }

        /// <summary>
        /// Gets the Cb chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public byte Cb { get; }

        /// <summary>
        /// Gets the Cr chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public byte Cr { get; }

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
            byte r = color.R;
            byte g = color.G;
            byte b = color.B;

            byte y = (byte)((0.299F * r) + (0.587F * g) + (0.114F * b));
            byte cb = (byte)(128 + ((-0.168736F * r) - (0.331264F * g) + (0.5F * b)));
            byte cr = (byte)(128 + ((0.5F * r) - (0.418688F * g) - (0.081312F * b)));

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
            unchecked
            {
                int hashCode = this.Y.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Cb.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Cr.GetHashCode();
                return hashCode;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "YCbCr [ Empty ]";
            }

            return $"YCbCr [ Y={this.Y}, Cb={this.Cb}, Cr={this.Cr} ]";
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
            return this.Y == other.Y && this.Cb == other.Cb && this.Cr == other.Cr;
        }
    }
}
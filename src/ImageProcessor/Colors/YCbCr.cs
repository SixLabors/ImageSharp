// --------------------------------------------------------------------------------------------------------------------
// <copyright file="YCbCr.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an YCbCr (luminance, chroma, chroma) color conforming to the
//   ITU-R BT.601 standard used in digital imaging systems.
//   <see href="http://en.wikipedia.org/wiki/YCbCr" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Represents an YCbCr (luminance, chroma, chroma) color conforming to the 
    /// ITU-R BT.601 standard used in digital imaging systems.
    /// <see href="http://en.wikipedia.org/wiki/YCbCr"/>
    /// </summary>
    public struct YCbCr : IEquatable<YCbCr>
    {
        /// <summary>
        /// Represents a <see cref="YCbCr"/> that has Y, Cb, and Cr values set to zero.
        /// </summary>
        public static readonly YCbCr Empty = new YCbCr();

        /// <summary>
        /// Holds the Y luminance component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Y { get; }

        /// <summary>
        /// Holds the Cb chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Cb { get; }

        /// <summary>
        /// Holds the Cr chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Cr { get; }

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCr"/> struct.
        /// </summary>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param> 
        public YCbCr(float y, float cb, float cr)
        {
            this.Y = y.Clamp(0, 255);
            this.Cb = cb.Clamp(0, 255);
            this.Cr = cr.Clamp(0, 255);
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="YCbCr"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => Math.Abs(this.Y) < Epsilon
                               && Math.Abs(this.Cb) < Epsilon
                               && Math.Abs(this.Cr) < Epsilon;

        /// <summary>
        /// Compares two <see cref="YCbCr"/> objects. The result specifies whether the values
        /// of the <see cref="YCbCr.Y"/>, <see cref="YCbCr.Cb"/>, and <see cref="YCbCr.Cr"/>
        /// properties of the two <see cref="YCbCr"/> objects are equal.
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
        /// Compares two <see cref="YCbCr"/> objects. The result specifies whether the values
        /// of the <see cref="YCbCr.Y"/>, <see cref="YCbCr.Cb"/>, and <see cref="YCbCr.Cr"/>
        /// properties of the two <see cref="YCbCr"/> objects are unequal.
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

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Bgra"/> to a 
        /// <see cref="YCbCr"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Bgra"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="YCbCr"/>.
        /// </returns>
        public static implicit operator YCbCr(Bgra color)
        {
            byte b = color.B;
            byte g = color.G;
            byte r = color.R;

            float y = (float)((0.299 * r) + (0.587 * g) + (0.114 * b));
            float cb = 128 + (float)((-0.168736 * r) - (0.331264 * g) + (0.5 * b));
            float cr = 128 + (float)((0.5 * r) - (0.418688 * g) - (0.081312 * b));

            return new YCbCr(y, cb, cr);
        }

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="YCbCr"/> to a 
        /// <see cref="Bgra"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="YCbCr"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Bgra"/>.
        /// </returns>
        public static implicit operator Bgra(YCbCr color)
        {
            float y = color.Y;
            float cb = color.Cb - 128;
            float cr = color.Cr - 128;

            byte b = Convert.ToByte((y + (1.772 * cb)).Clamp(0, 255));
            byte g = Convert.ToByte((y - (0.34414 * cb) - (0.71414 * cr)).Clamp(0, 255));
            byte r = Convert.ToByte((y + (1.402 * cr)).Clamp(0, 255));

            return new Bgra(b, g, r, 255);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.
        /// </returns>
        /// <param name="obj">Another object to compare to. </param>
        public override bool Equals(object obj)
        {
            if (obj is YCbCr)
            {
                YCbCr color = (YCbCr)obj;

                return Math.Abs(this.Y - color.Y) < Epsilon
                    && Math.Abs(this.Cb - color.Cb) < Epsilon
                    && Math.Abs(this.Cr - color.Cr) < Epsilon;
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
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

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "YCbCrColor [ Empty ]";
            }

            return $"YCbCrColor [ Y={this.Y:#0.##}, Cb={this.Cb:#0.##}, Cr={this.Cr:#0.##} ]";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(YCbCr other)
        {
            return this.Y.Equals(other.Y)
                && this.Cb.Equals(other.Cb)
                && this.Cr.Equals(other.Cr);
        }
    }
}

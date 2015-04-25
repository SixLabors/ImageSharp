// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Size.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Stores an ordered pair of integers, which specify a height and width.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Globalization;

    /// <summary>
    /// Stores an ordered pair of integers, which specify a height and width.
    /// </summary>
    public struct Size : IEquatable<Size>
    {
        /// <summary>
        /// Represents a <see cref="Size"/> that has Width and Height values set to zero.
        /// </summary>
        public static readonly Size Empty = new Size();

        /// <summary>
        /// The width of this <see cref="Size"/>.
        /// </summary>
        public int Width;

        /// <summary>
        /// The height of this <see cref="Size"/>.
        /// </summary>
        public int Height;

        /// <summary>
        /// Initializes a new instance of the <see cref="Size"/> struct.
        /// </summary>
        /// <param name="width">
        /// The width of the size. 
        /// </param>
        /// <param name="height">
        /// The height of the size. 
        /// </param>
        public Size(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Size"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty
        {
            get
            {
                return this.Width == 0 && this.Height == 0;
            }
        }

        /// <summary>
        /// Compares two <see cref="Size"/> objects. The result specifies whether the values
        /// <see cref="Size.Width"/> and the <see cref="Size.Height"/>properties of the two
        /// <see cref="Size"/> objects are equal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Size"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Size"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Size left, Size right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Size"/> objects. The result specifies whether the values
        /// <see cref="Size.Width"/> and the <see cref="Size.Height"/>properties of the two
        /// <see cref="Size"/> objects are unequal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Size"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Size"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Size left, Size right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// True if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false. 
        /// </returns>
        /// <param name="obj">The object to compare with the current instance. </param>
        public override bool Equals(object obj)
        {
            if (!(obj is Size))
            {
                return false;
            }

            Size other = (Size)obj;

            return other.Width == this.Width && other.Height == this.Height;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            return this.GetHashCode(this);
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            return "{Width=" + this.Width.ToString(CultureInfo.CurrentCulture)
                   + ",Height=" + this.Height.ToString(CultureInfo.CurrentCulture) + "}";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Size other)
        {
            return this.Width.Equals(other.Width) && this.Height.Equals(other.Height);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="size">
        /// The instance of <see cref="Size"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Size size)
        {
            unchecked
            {
                int hashCode = size.Width.GetHashCode();
                hashCode = (hashCode * 397) ^ size.Height.GetHashCode();
                return hashCode;
            }
        }
    }
}

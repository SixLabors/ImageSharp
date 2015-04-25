// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Point.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents an ordered pair of integer x- and y-coordinates that defines a point in
//   a two-dimensional plane.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Globalization;

    /// <summary>
    /// Represents an ordered pair of integer x- and y-coordinates that defines a point in 
    /// a two-dimensional plane.
    /// </summary>
    public struct Point : IEquatable<Point>
    {
        /// <summary>
        /// Represents a <see cref="Point"/> that has X and Y values set to zero.
        /// </summary>
        public static readonly Point Empty = new Point();

        /// <summary>
        /// The x-coordinate of this <see cref="Point"/>.
        /// </summary>
        public int X;

        /// <summary>
        /// The y-coordinate of this <see cref="Point"/>.
        /// </summary>
        public int Y;

        /// <summary>
        /// Initializes a new instance of the <see cref="Point"/> struct.
        /// </summary>
        /// <param name="x">
        /// The horizontal position of the point. 
        /// </param>
        /// <param name="y">
        /// The vertical position of the point. 
        /// </param>
        public Point(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Point"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty
        {
            get
            {
                return this.X == 0 && this.Y == 0;
            }
        }

        /// <summary>
        /// Compares two <see cref="Point"/> objects. The result specifies whether the values
        /// of the <see cref="Point.X"/> or <see cref="Point.Y"/> properties of the two
        /// <see cref="Point"/> objects are equal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Point"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Point"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Point left, Point right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Point"/> objects. The result specifies whether the values
        /// of the <see cref="Point.X"/> or <see cref="Point.Y"/> properties of the two
        /// <see cref="Point"/> objects are unequal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Point"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Point"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Point left, Point right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <param name="obj">
        /// The object to compare with the current instance. 
        /// </param>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the 
        /// same value; otherwise, false. 
        /// </returns>
        public override bool Equals(object obj)
        {
            if (!(obj is Point))
            {
                return false;
            }

            Point other = (Point)obj;

            return other.X == this.X && other.Y == this.Y;
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
            return "{X=" + this.X.ToString(CultureInfo.CurrentCulture) + ",Y=" + this.Y.ToString(CultureInfo.CurrentCulture) + "}";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Point other)
        {
            return this.X.Equals(other.X) && this.Y.Equals(other.Y);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="point">
        /// The instance of <see cref="Point"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Point point)
        {
            unchecked
            {
                int hashCode = point.X.GetHashCode();
                hashCode = (hashCode * 397) ^ point.Y.GetHashCode();
                return hashCode;
            }
        }
    }
}

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

    /// <summary>
    /// Represents an ordered pair of integer x- and y-coordinates that defines a point in 
    /// a two-dimensional plane.
    /// </summary>
    public struct Point : IEquatable<Point>
    {
        /// <summary>
        /// Represents a Point that has X and Y values set to zero.
        /// </summary>
        public static readonly Point Empty = new Point();

        /// <summary>
        /// The x-coordinate of this Point.
        /// </summary>
        public int X;

        /// <summary>
        /// The y-coordinate of this Point.
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
        /// Gets a value indicating whether this Point is empty.
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
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Point other)
        {
            return this.X.Equals(other.X) && this.Y.Equals(other.Y);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="obj">
        /// The instance of <see cref="Point"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Point obj)
        {
            unchecked
            {
                int hashCode = obj.X.GetHashCode();
                hashCode = (hashCode * 397) ^ obj.Y.GetHashCode();
                return hashCode;
            }
        }
    }
}

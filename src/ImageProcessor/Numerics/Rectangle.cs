// <copyright file="Rectangle.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Stores a set of four integers that represent the location and size of a rectangle.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct Rectangle : IEquatable<Rectangle>
    {
        /// <summary>
        /// Represents a <see cref="Rectangle"/> that has X, Y, Width, and Height values set to zero.
        /// </summary>
        public static readonly Rectangle Empty = default(Rectangle);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rectangle"/> struct.
        /// </summary>
        /// <param name="x">The horizontal position of the rectangle.</param>
        /// <param name="y">The vertical position of the rectangle.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        public Rectangle(int x, int y, int width, int height)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rectangle"/> struct.
        /// </summary>
        /// <param name="point">
        /// The <see cref="Point"/> which specifies the rectangles point in a two-dimensional plane.
        /// </param>
        /// <param name="size">
        /// The <see cref="Size"/> which specifies the rectangles height and width.
        /// </param>
        public Rectangle(Point point, Size size)
        {
            this.X = point.X;
            this.Y = point.Y;
            this.Width = size.Width;
            this.Height = size.Height;
        }

        /// <summary>
        /// The x-coordinate of this <see cref="Rectangle"/>.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// The y-coordinate of this <see cref="Rectangle"/>.
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// The width of this <see cref="Rectangle"/>.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// The height of this <see cref="Rectangle"/>.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Rectangle"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.X == 0 && this.Y == 0 && this.Width == 0 && this.Height == 0;

        /// <summary>
        /// Gets the y-coordinate of the top edge of this <see cref="Rectangle"/>.
        /// </summary>
        public int Top => this.Y;

        /// <summary>
        /// Gets the x-coordinate of the right edge of this <see cref="Rectangle"/>.
        /// </summary>
        public int Right => this.X + this.Width;

        /// <summary>
        /// Gets the y-coordinate of the bottom edge of this <see cref="Rectangle"/>.
        /// </summary>
        public int Bottom => this.Y + this.Height;

        /// <summary>
        /// Gets the x-coordinate of the left edge of this <see cref="Rectangle"/>.
        /// </summary>
        public int Left => this.X;

        /// <summary>
        /// Compares two <see cref="Rectangle"/> objects. The result specifies whether the values
        /// of the <see cref="Rectangle.X"/>, <see cref="Rectangle.Y"/>, <see cref="Rectangle.Width"/>,
        /// and the <see cref="Rectangle.Height"/>properties of the two
        /// <see cref="Rectangle"/> objects are equal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rectangle"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rectangle"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Rectangle left, Rectangle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Rectangle"/> objects. The result specifies whether the values
        /// of the <see cref="Rectangle.X"/>, <see cref="Rectangle.Y"/>, <see cref="Rectangle.Width"/>,
        /// and the <see cref="Rectangle.Height"/>properties of the two
        /// <see cref="Rectangle"/> objects are unequal.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rectangle"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rectangle"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Rectangle left, Rectangle right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines if the specfied point is contained within the rectangular region defined by
        /// this <see cref="Rectangle"/>.
        /// </summary>
        /// <param name="x">The x-coordinate of the given point.</param>
        /// <param name="y">The y-coordinate of the given point.</param>
        /// <returns>The <see cref="bool"/></returns>
        public bool Contains(int x, int y)
        {
            return this.X <= x
                   && x < this.X + this.Width
                   && this.Y <= y
                   && y < this.Y + this.Height;
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
            if (!(obj is Rectangle))
            {
                return false;
            }

            Rectangle other = (Rectangle)obj;

            return other.X == this.X && other.Y == this.Y
                   && other.Width == this.Width && other.Height == this.Height;
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
            if (this.IsEmpty)
            {
                return "Rectangle [ Empty ]";
            }

            return
                $"Rectangle [ X={this.X}, Y={this.Y}, Width={this.Width}, Height={this.Height} ]";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Rectangle other)
        {
            return this.X.Equals(other.X)
                && this.Y.Equals(other.Y)
                && this.Width.Equals(other.Width)
                && this.Height.Equals(other.Height);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="rectangle">
        /// The instance of <see cref="Rectangle"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Rectangle rectangle)
        {
            unchecked
            {
                int hashCode = rectangle.X.GetHashCode();
                hashCode = (hashCode * 397) ^ rectangle.Y.GetHashCode();
                hashCode = (hashCode * 397) ^ rectangle.Width.GetHashCode();
                hashCode = (hashCode * 397) ^ rectangle.Height.GetHashCode();
                return hashCode;
            }
        }
    }
}

// <copyright file="Rectangle.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

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
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector4 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rectangle"/> struct.
        /// </summary>
        /// <param name="x">The horizontal position of the rectangle.</param>
        /// <param name="y">The vertical position of the rectangle.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        public Rectangle(int x, int y, int width, int height)
        {
            this.backingVector = new Vector4(x, y, width, height);
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
            this.backingVector = new Vector4(point.X, point.Y, size.Width, size.Height);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rectangle"/> struct.
        /// </summary>
        /// <param name="vector">The vector.</param>
        public Rectangle(Vector4 vector)
        {
            this.backingVector = vector;
        }

        /// <summary>
        /// The x-coordinate of this <see cref="Rectangle"/>.
        /// </summary>
        public int X
        {
            get
            {
                return (int)this.backingVector.X;
            }

            set
            {
                this.backingVector.X = value;
            }
        }

        /// <summary>
        /// The y-coordinate of this <see cref="Rectangle"/>.
        /// </summary>
        public int Y
        {
            get
            {
                return (int)this.backingVector.Y;
            }

            set
            {
                this.backingVector.Y = value;
            }
        }

        /// <summary>
        /// The width of this <see cref="Rectangle"/>.
        /// </summary>
        public int Width
        {
            get
            {
                return (int)this.backingVector.Z;
            }

            set
            {
                this.backingVector.Z = value;
            }
        }

        /// <summary>
        /// The height of this <see cref="Rectangle"/>.
        /// </summary>
        public int Height
        {
            get
            {
                return (int)this.backingVector.W;
            }

            set
            {
                this.backingVector.W = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Rectangle"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

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
        /// Computes the sum of adding two rectangles.
        /// </summary>
        /// <param name="left">The rectangle on the left hand of the operand.</param>
        /// <param name="right">The rectangle on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>
        /// </returns>
        public static Rectangle operator +(Rectangle left, Rectangle right)
        {
            return new Rectangle(left.backingVector + right.backingVector);
        }

        /// <summary>
        /// Computes the difference left by subtracting one rectangle from another.
        /// </summary>
        /// <param name="left">The rectangle on the left hand of the operand.</param>
        /// <param name="right">The rectangle on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Rectangle"/>
        /// </returns>
        public static Rectangle operator -(Rectangle left, Rectangle right)
        {
            return new Rectangle(left.backingVector - right.backingVector);
        }

        /// <summary>
        /// Compares two <see cref="Rectangle"/> objects for equality.
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
        /// Compares two <see cref="Rectangle"/> objects for inequality.
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
            // TODO: SIMD?
            return this.X <= x
                   && x < this.Right
                   && this.Y <= y
                   && y < this.Bottom;
        }

        /// <summary>
        /// Returns the center point of the given <see cref="Rectangle"/>
        /// </summary>
        /// <param name="rectangle">The rectangle</param>
        /// <returns><see cref="Point"/></returns>
        public static Point Center(Rectangle rectangle)
        {
            return new Point(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.GetHashCode(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "Rectangle [ Empty ]";
            }

            return
                $"Rectangle [ X={this.X}, Y={this.Y}, Width={this.Width}, Height={this.Height} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is Rectangle)
            {
                return this.Equals((Rectangle)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(Rectangle other)
        {
            return this.backingVector.Equals(other.backingVector);
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
            return rectangle.backingVector.GetHashCode();
        }
    }
}

// <copyright file="RectangleF.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
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
    public struct RectangleF : IEquatable<RectangleF>
    {
        /// <summary>
        /// Represents a <see cref="Rectangle"/> that has X, Y, Width, and Height values set to zero.
        /// </summary>
        public static readonly RectangleF Empty = default(RectangleF);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector4 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="RectangleF"/> struct.
        /// </summary>
        /// <param name="x">The horizontal position of the rectangle.</param>
        /// <param name="y">The vertical position of the rectangle.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        public RectangleF(float x, float y, float width, float height)
        {
            this.backingVector = new Vector4(x, y, width, height);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RectangleF"/> struct.
        /// </summary>
        /// <param name="vector">The vector.</param>
        public RectangleF(Vector4 vector)
        {
            this.backingVector = vector;
        }

        /// <summary>
        /// Gets or sets the x-coordinate of this <see cref="RectangleF"/>.
        /// </summary>
        public float X
        {
            get
            {
                return this.backingVector.X;
            }

            set
            {
                this.backingVector.X = value;
            }
        }

        /// <summary>
        /// Gets or sets the y-coordinate of this <see cref="RectangleF"/>.
        /// </summary>
        public float Y
        {
            get
            {
                return this.backingVector.Y;
            }

            set
            {
                this.backingVector.Y = value;
            }
        }

        /// <summary>
        /// Gets or sets the width of this <see cref="RectangleF"/>.
        /// </summary>
        public float Width
        {
            get
            {
                return this.backingVector.Z;
            }

            set
            {
                this.backingVector.Z = value;
            }
        }

        /// <summary>
        /// Gets or sets the height of this <see cref="RectangleF"/>.
        /// </summary>
        public float Height
        {
            get
            {
                return this.backingVector.W;
            }

            set
            {
                this.backingVector.W = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="RectangleF"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Gets the y-coordinate of the top edge of this <see cref="RectangleF"/>.
        /// </summary>
        public float Top => this.Y;

        /// <summary>
        /// Gets the x-coordinate of the right edge of this <see cref="RectangleF"/>.
        /// </summary>
        public float Right => this.X + this.Width;

        /// <summary>
        /// Gets the y-coordinate of the bottom edge of this <see cref="RectangleF"/>.
        /// </summary>
        public float Bottom => this.Y + this.Height;

        /// <summary>
        /// Gets the x-coordinate of the left edge of this <see cref="RectangleF"/>.
        /// </summary>
        public float Left => this.X;

        /// <summary>
        /// Performs an implicit conversion from <see cref="Rectangle"/> to <see cref="RectangleF"/>.
        /// </summary>
        /// <param name="d">The d.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator RectangleF(Rectangle d)
        {
            return new RectangleF(d.Left, d.Top, d.Width, d.Height);
        }

        /// <summary>
        /// Computes the sum of adding two rectangles.
        /// </summary>
        /// <param name="left">The rectangle on the left hand of the operand.</param>
        /// <param name="right">The rectangle on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="RectangleF"/>
        /// </returns>
        public static RectangleF operator +(RectangleF left, RectangleF right)
        {
            return new RectangleF(left.backingVector + right.backingVector);
        }

        /// <summary>
        /// Computes the difference left by subtracting one rectangle from another.
        /// </summary>
        /// <param name="left">The rectangle on the left hand of the operand.</param>
        /// <param name="right">The rectangle on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="RectangleF"/>
        /// </returns>
        public static RectangleF operator -(RectangleF left, RectangleF right)
        {
            return new RectangleF(left.backingVector - right.backingVector);
        }

        /// <summary>
        /// Compares two <see cref="RectangleF"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="RectangleF"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="RectangleF"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(RectangleF left, RectangleF right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="RectangleF"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="RectangleF"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="RectangleF"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(RectangleF left, RectangleF right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Returns the center point of the given <see cref="RectangleF"/>
        /// </summary>
        /// <param name="rectangle">The rectangle</param>
        /// <returns><see cref="Point"/></returns>
        public static Vector2 Center(RectangleF rectangle)
        {
            return new Vector2(rectangle.Left + (rectangle.Width / 2), rectangle.Top + (rectangle.Height / 2));
        }

        /// <summary>
        /// Rounds the points away from the center this into a <see cref="Rectangle"/>
        /// by rounding the dimensions to the nerent integer ensuring that the new rectangle is
        /// never smaller than the source <see cref="RectangleF"/>
        /// </summary>
        /// <param name="source">The source area to round out</param>
        /// <returns>
        ///     The smallest <see cref="Rectangle"/> that the <see cref="RectangleF"/> will fit inside.
        /// </returns>
        public static Rectangle Ceiling(RectangleF source)
        {
            int y = (int)Math.Floor(source.Y);
            int width = (int)Math.Ceiling(source.Width);
            int x = (int)Math.Floor(source.X);
            int height = (int)Math.Ceiling(source.Height);
            return new Rectangle(x, y, width, height);
        }

        /// <summary>
        /// Outsets the specified region.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <param name="width">The width.</param>
        /// <returns>
        /// The <see cref="RectangleF"/> with all dimensions move away from the center by the offset.
        /// </returns>
        public static RectangleF Outset(RectangleF region, float width)
        {
            float dblWidth = width * 2;
            return new RectangleF(region.X - width, region.Y - width, region.Width + dblWidth, region.Height + dblWidth);
        }

        /// <summary>
        /// Determines if the specfied point is contained within the rectangular region defined by
        /// this <see cref="RectangleF"/>.
        /// </summary>
        /// <param name="x">The x-coordinate of the given point.</param>
        /// <param name="y">The y-coordinate of the given point.</param>
        /// <returns>The <see cref="bool"/></returns>
        public bool Contains(float x, float y)
        {
            // TODO: SIMD?
            return this.X <= x
                   && x < this.Right
                   && this.Y <= y
                   && y < this.Bottom;
        }

        /// <summary>
        /// Determines if the specfied <see cref="Rectangle"/> intersects the rectangular region defined by
        /// this <see cref="Rectangle"/>.
        /// </summary>
        /// <param name="rect">The other Rectange </param>
        /// <returns>The <see cref="bool"/></returns>
        public bool Intersects(RectangleF rect)
        {
            return rect.Left <= this.Right && rect.Right >= this.Left
                &&
                rect.Top <= this.Bottom && rect.Bottom >= this.Top;
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
            if (obj is RectangleF)
            {
                return this.Equals((RectangleF)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(RectangleF other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="rectangle">
        /// The instance of <see cref="RectangleF"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(RectangleF rectangle)
        {
            return rectangle.backingVector.GetHashCode();
        }
    }
}

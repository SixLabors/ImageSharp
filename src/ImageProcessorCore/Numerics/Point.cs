// <copyright file="Point.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents an ordered pair of integer x- and y-coordinates that defines a point in
    /// a two-dimensional plane.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct Point : IEquatable<Point>
    {
        /// <summary>
        /// Represents a <see cref="Point"/> that has X and Y values set to zero.
        /// </summary>
        public static readonly Point Empty = default(Point);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector2 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="Point"/> struct.
        /// </summary>
        /// <param name="x">The horizontal position of the point.</param>
        /// <param name="y">The vertical position of the point.</param>
        public Point(int x, int y)
            : this()
        {
            this.backingVector = new Vector2(x, y);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Point"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector representing the width and height.
        /// </param>
        public Point(Vector2 vector)
        {
            this.backingVector = new Vector2(vector.X, vector.Y);
        }

        /// <summary>
        /// The x-coordinate of this <see cref="Point"/>.
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
        /// The y-coordinate of this <see cref="Point"/>.
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
        /// Gets a value indicating whether this <see cref="Point"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Computes the sum of adding two points.
        /// </summary>
        /// <param name="left">The point on the left hand of the operand.</param>
        /// <param name="right">The point on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Point"/>
        /// </returns>
        public static Point operator +(Point left, Point right)
        {
            return new Point(left.backingVector + right.backingVector);
        }

        /// <summary>
        /// Computes the difference left by subtracting one point from another.
        /// </summary>
        /// <param name="left">The point on the left hand of the operand.</param>
        /// <param name="right">The point on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Point"/>
        /// </returns>
        public static Point operator -(Point left, Point right)
        {
            return new Point(left.backingVector - right.backingVector);
        }

        /// <summary>
        /// Compares two <see cref="Point"/> objects for equality.
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
        /// Compares two <see cref="Point"/> objects for inequality.
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
        /// Gets a <see cref="Vector2"/> representation for this <see cref="Point"/>.
        /// </summary>
        /// <returns>A <see cref="Vector2"/> representation for this object.</returns>
        public Vector2 ToVector2()
        {
            return new Vector2(this.X, this.Y);
        }

        /// <summary>
        /// Creates a rotation matrix for the given point and angle.
        /// </summary>
        /// <param name="origin">The origin point to rotate around</param>
        /// <param name="degrees">Rotation in degrees</param>
        /// <returns>The rotation <see cref="Matrix3x2"/></returns>
        public static Matrix3x2 CreateRotation(Point origin, float degrees)
        {
            float radians = (float)ImageMaths.DegreesToRadians(degrees);
            return Matrix3x2.CreateRotation(radians, origin.backingVector);
        }

        /// <summary>
        /// Rotates a point around a given a rotation matrix.
        /// </summary>
        /// <param name="point">The point to rotate</param>
        /// <param name="rotation">Rotation matrix used</param>
        /// <returns>The rotated <see cref="Point"/></returns>
        public static Point Rotate(Point point, Matrix3x2 rotation)
        {
            return new Point(Vector2.Transform(point.backingVector, rotation));
        }

        /// <summary>
        /// Rotates a point around a given origin by the specified angle in degrees.
        /// </summary>
        /// <param name="point">The point to rotate</param>
        /// <param name="origin">The center point to rotate around.</param>
        /// <param name="degrees">The angle in degrees.</param>
        /// <returns>The rotated <see cref="Point"/></returns>
        public static Point Rotate(Point point, Point origin, float degrees)
        {
            return new Point(Vector2.Transform(point.backingVector, CreateRotation(origin, degrees)));
        }

        /// <summary>
        /// Creates a skew matrix for the given point and angle.
        /// </summary>
        /// <param name="origin">The origin point to rotate around</param>
        /// <param name="degreesX">The x-angle in degrees.</param>
        /// <param name="degreesY">The y-angle in degrees.</param>
        /// <returns>The rotation <see cref="Matrix3x2"/></returns>
        public static Matrix3x2 CreateSkew(Point origin, float degreesX, float degreesY)
        {
            float radiansX = (float)ImageMaths.DegreesToRadians(degreesX);
            float radiansY = (float)ImageMaths.DegreesToRadians(degreesY);
            return Matrix3x2.CreateSkew(radiansX, radiansY, origin.backingVector);
        }

        /// <summary>
        /// Skews a point using a given a skew matrix.
        /// </summary>
        /// <param name="point">The point to rotate</param>
        /// <param name="skew">Rotation matrix used</param>
        /// <returns>The rotated <see cref="Point"/></returns>
        public static Point Skew(Point point, Matrix3x2 skew)
        {
            return new Point(Vector2.Transform(point.backingVector, skew));
        }

        /// <summary>
        /// Skews a point around a given origin by the specified angles in degrees.
        /// </summary>
        /// <param name="point">The point to skew.</param>
        /// <param name="origin">The center point to rotate around.</param>
        /// <param name="degreesX">The x-angle in degrees.</param>
        /// <param name="degreesY">The y-angle in degrees.</param>
        /// <returns>The skewed <see cref="Point"/></returns>
        public static Point Skew(Point point, Point origin, float degreesX, float degreesY)
        {
            return new Point(Vector2.Transform(point.backingVector, CreateSkew(origin, degreesX, degreesY)));
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
                return "Point [ Empty ]";
            }

            return $"Point [ X={this.X}, Y={this.Y} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is Point)
            {
                return this.Equals((Point)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(Point other)
        {
            return this.backingVector.Equals(other.backingVector);
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
            return point.backingVector.GetHashCode();
        }
    }
}

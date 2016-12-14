// <copyright file="PointF.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.ComponentModel;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents an ordered pair of floating point x- and y-coordinates that defines a point in
    /// a two-dimensional plane.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct PointF : IEquatable<PointF>
    {
        /// <summary>
        /// Represents a <see cref="Point"/> that has X and Y values set to zero.
        /// </summary>
        public static readonly PointF Empty = default(PointF);

        private Vector2 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="PointF"/> struct.
        /// </summary>
        /// <param name="x">The horizontal position of the point.</param>
        /// <param name="y">The vertical position of the point.</param>
        public PointF(float x, float y)
            : this(new Vector2(x, y))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PointF"/> struct.
        /// </summary>
        /// <param name="vector">
        /// The vector representing the width and height.
        /// </param>
        public PointF(Vector2 vector)
        {
            this.backingVector = vector;
        }

        /// <summary>
        /// Gets or sets the x-coordinate of this <see cref="PointF"/>.
        /// </summary>
        public float X
        {
            get { return this.backingVector.X; }
            set { this.backingVector.X = value; }
        }

        /// <summary>
        /// Gets or sets the y-coordinate of this <see cref="PointF"/>.
        /// </summary>
        public float Y
        {
            get { return this.backingVector.Y; }
            set { this.backingVector.Y = value; }
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="PointF"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Performs an implicit conversion from <see cref="Point"/> to <see cref="PointF"/>.
        /// </summary>
        /// <param name="d">The d.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator PointF(Point d)
        {
            return new PointF(d.ToVector2());
        }

        /// <summary>
        /// Computes the sum of adding two points.
        /// </summary>
        /// <param name="left">The point on the left hand of the operand.</param>
        /// <param name="right">The point on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Point"/>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF operator +(PointF left, PointF right)
        {
            return new PointF(left.backingVector + right.backingVector);
        }

        /// <summary>
        /// Computes the difference left by subtracting one point from another.
        /// </summary>
        /// <param name="left">The point on the left hand of the operand.</param>
        /// <param name="right">The point on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="PointF"/>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF operator -(PointF left, PointF right)
        {
            return new PointF(left.backingVector - right.backingVector);
        }

        /// <summary>
        /// Compares two <see cref="PointF"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="PointF"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="PointF"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(PointF left, PointF right)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(PointF left, PointF right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Creates a rotation matrix for the given point and angle.
        /// </summary>
        /// <param name="origin">The origin point to rotate around</param>
        /// <param name="degrees">Rotation in degrees</param>
        /// <returns>The rotation <see cref="Matrix3x2"/></returns>
        public static Matrix3x2 CreateRotation(PointF origin, float degrees)
        {
            float radians = ImageMaths.DegreesToRadians(degrees);
            return Matrix3x2.CreateRotation(radians, new Vector2(origin.X, origin.Y));
        }

        /// <summary>
        /// Rotates a point around a given a rotation matrix.
        /// </summary>
        /// <param name="point">The point to rotate</param>
        /// <param name="rotation">Rotation matrix used</param>
        /// <returns>The rotated <see cref="Point"/></returns>
        public static PointF Rotate(PointF point, Matrix3x2 rotation)
        {
            return new PointF(Vector2.Transform(new Vector2(point.X, point.Y), rotation));
        }

        /// <summary>
        /// Rotates a point around a given origin by the specified angle in degrees.
        /// </summary>
        /// <param name="point">The point to rotate</param>
        /// <param name="origin">The center point to rotate around.</param>
        /// <param name="degrees">The angle in degrees.</param>
        /// <returns>The rotated <see cref="Point"/></returns>
        public static PointF Rotate(PointF point, PointF origin, float degrees)
        {
            return new PointF(Vector2.Transform(new Vector2(point.X, point.Y), CreateRotation(origin, degrees)));
        }

        /// <summary>
        /// Creates a skew matrix for the given point and angle.
        /// </summary>
        /// <param name="origin">The origin point to rotate around</param>
        /// <param name="degreesX">The x-angle in degrees.</param>
        /// <param name="degreesY">The y-angle in degrees.</param>
        /// <returns>The rotation <see cref="Matrix3x2"/></returns>
        public static Matrix3x2 CreateSkew(PointF origin, float degreesX, float degreesY)
        {
            float radiansX = ImageMaths.DegreesToRadians(degreesX);
            float radiansY = ImageMaths.DegreesToRadians(degreesY);
            return Matrix3x2.CreateSkew(radiansX, radiansY, origin.backingVector);
        }

        /// <summary>
        /// Skews a point using a given a skew matrix.
        /// </summary>
        /// <param name="point">The point to rotate</param>
        /// <param name="skew">Rotation matrix used</param>
        /// <returns>The rotated <see cref="Point"/></returns>
        public static PointF Skew(PointF point, Matrix3x2 skew)
        {
            return new PointF(Vector2.Transform(point.backingVector, skew));
        }

        /// <summary>
        /// Skews a point around a given origin by the specified angles in degrees.
        /// </summary>
        /// <param name="point">The point to skew.</param>
        /// <param name="origin">The center point to rotate around.</param>
        /// <param name="degreesX">The x-angle in degrees.</param>
        /// <param name="degreesY">The y-angle in degrees.</param>
        /// <returns>The skewed <see cref="Point"/></returns>
        public static PointF Skew(PointF point, PointF origin, float degreesX, float degreesY)
        {
            return new PointF(Vector2.Transform(point.backingVector, CreateSkew(origin, degreesX, degreesY)));
        }

        /// <summary>
        /// Gets a <see cref="Vector2"/> representation for this <see cref="Point"/>.
        /// </summary>
        /// <returns>A <see cref="Vector2"/> representation for this object.</returns>
        public Vector2 ToVector2()
        {
            // should this be a return of the mutable vector2 backing vector instead of a copy?
            return new Vector2(this.X, this.Y);
        }

        /// <summary>
        /// Translates this <see cref="Point"/> by the specified amount.
        /// </summary>
        /// <param name="dx">The amount to offset the x-coordinate.</param>
        /// <param name="dy">The amount to offset the y-coordinate.</param>
        public void Offset(float dx, float dy)
        {
            this.backingVector += new Vector2(dx, dy);
        }

        /// <summary>
        /// Translates this <see cref="PointF"/> by the specified amount.
        /// </summary>
        /// <param name="p">The <see cref="PointF"/> used offset this <see cref="PointF"/>.</param>
        public void Offset(PointF p)
        {
            this.backingVector += p.backingVector;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.backingVector.GetHashCode();
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
            if (obj is PointF)
            {
                return this.Equals((PointF)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(PointF other)
        {
            return this.backingVector == other.backingVector;
        }
    }
}
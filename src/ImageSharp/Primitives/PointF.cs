// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents an ordered pair of single precision floating point x- and y-coordinates that defines a point in
    /// a two-dimensional plane.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct PointF : IEquatable<PointF>
    {
        /// <summary>
        /// Represents a <see cref="PointF"/> that has X and Y values set to zero.
        /// </summary>
        public static readonly PointF Empty = default;

        /// <summary>
        /// Initializes a new instance of the <see cref="PointF"/> struct.
        /// </summary>
        /// <param name="x">The horizontal position of the point.</param>
        /// <param name="y">The vertical position of the point.</param>
        public PointF(float x, float y)
            : this()
        {
            this.X = x;
            this.Y = y;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PointF"/> struct from the given <see cref="SizeF"/>.
        /// </summary>
        /// <param name="size">The size.</param>
        public PointF(SizeF size)
        {
            this.X = size.Width;
            this.Y = size.Height;
        }

        /// <summary>
        /// Gets or sets the x-coordinate of this <see cref="PointF"/>.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Gets or sets the y-coordinate of this <see cref="PointF"/>.
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="PointF"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Creates a <see cref="Vector2"/> with the coordinates of the specified <see cref="PointF"/>.
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>
        /// The <see cref="Vector2"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PointF(Vector2 vector) => new PointF(vector.X, vector.Y);

        /// <summary>
        /// Creates a <see cref="Vector2"/> with the coordinates of the specified <see cref="PointF"/>.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The <see cref="Vector2"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector2(PointF point) => new Vector2(point.X, point.Y);

        /// <summary>
        /// Creates a <see cref="Point"/> with the coordinates of the specified <see cref="PointF"/> by truncating each of the coordinates.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The <see cref="Point"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Point(PointF point) => Point.Truncate(point);

        /// <summary>
        /// Negates the given point by multiplying all values by -1.
        /// </summary>
        /// <param name="value">The source point.</param>
        /// <returns>The negated point.</returns>
        public static PointF operator -(PointF value) => new PointF(-value.X, -value.Y);

        /// <summary>
        /// Translates a <see cref="PointF"/> by a given <see cref="SizeF"/>.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="size">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="PointF"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF operator +(PointF point, SizeF size) => Add(point, size);

        /// <summary>
        /// Translates a <see cref="PointF"/> by the negative of a given <see cref="SizeF"/>.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="size">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="PointF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF operator -(PointF point, PointF size) => Subtract(point, size);

        /// <summary>
        /// Translates a <see cref="PointF"/> by a given <see cref="SizeF"/>.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="size">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="PointF"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF operator +(PointF point, PointF size) => Add(point, size);

        /// <summary>
        /// Translates a <see cref="PointF"/> by the negative of a given <see cref="SizeF"/>.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="size">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="PointF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF operator -(PointF point, SizeF size) => Subtract(point, size);

        /// <summary>
        /// Multiplies <see cref="PointF"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Multiplier of type <see cref="float"/>.</param>
        /// <param name="right">Multiplicand of type <see cref="SizeF"/>.</param>
        /// <returns>Product of type <see cref="SizeF"/>.</returns>
        public static PointF operator *(float left, PointF right) => Multiply(right, left);

        /// <summary>
        /// Multiplies <see cref="PointF"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Multiplicand of type <see cref="PointF"/>.</param>
        /// <param name="right">Multiplier of type <see cref="float"/>.</param>
        /// <returns>Product of type <see cref="SizeF"/>.</returns>
        public static PointF operator *(PointF left, float right) => Multiply(left, right);

        /// <summary>
        /// Divides <see cref="PointF"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Dividend of type <see cref="PointF"/>.</param>
        /// <param name="right">Divisor of type <see cref="int"/>.</param>
        /// <returns>Result of type <see cref="PointF"/>.</returns>
        public static PointF operator /(PointF left, float right)
            => new PointF(left.X / right, left.Y / right);

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
        public static bool operator ==(PointF left, PointF right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="PointF"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="PointF"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="PointF"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(PointF left, PointF right) => !left.Equals(right);

        /// <summary>
        /// Translates a <see cref="PointF"/> by the given <see cref="SizeF"/>.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="size">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="PointF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF Add(PointF point, SizeF size) => new PointF(point.X + size.Width, point.Y + size.Height);

        /// <summary>
        /// Translates a <see cref="PointF"/> by the given <see cref="PointF"/>.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="pointb">The point on the right hand of the operand.</param>
        /// <returns>The <see cref="PointF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF Add(PointF point, PointF pointb) => new PointF(point.X + pointb.X, point.Y + pointb.Y);

        /// <summary>
        /// Translates a <see cref="PointF"/> by the negative of a given <see cref="SizeF"/>.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="size">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="PointF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF Subtract(PointF point, SizeF size) => new PointF(point.X - size.Width, point.Y - size.Height);

        /// <summary>
        /// Translates a <see cref="PointF"/> by the negative of a given <see cref="PointF"/>.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="pointb">The point on the right hand of the operand.</param>
        /// <returns>The <see cref="PointF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF Subtract(PointF point, PointF pointb) => new PointF(point.X - pointb.X, point.Y - pointb.Y);

        /// <summary>
        /// Translates a <see cref="PointF"/> by the multiplying the X and Y by the given value.
        /// </summary>
        /// <param name="point">The point on the left hand of the operand.</param>
        /// <param name="right">The value on the right hand of the operand.</param>
        /// <returns>The <see cref="PointF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF Multiply(PointF point, float right) => new PointF(point.X * right, point.Y * right);

        /// <summary>
        /// Transforms a point by a specified 3x2 matrix.
        /// </summary>
        /// <param name="point">The point to transform.</param>
        /// <param name="matrix">The transformation matrix used.</param>
        /// <returns>The transformed <see cref="PointF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF Transform(PointF point, Matrix3x2 matrix) => Vector2.Transform(point, matrix);

        /// <summary>
        /// Deconstructs this point into two floats.
        /// </summary>
        /// <param name="x">The out value for X.</param>
        /// <param name="y">The out value for Y.</param>
        public void Deconstruct(out float x, out float y)
        {
            x = this.X;
            y = this.Y;
        }

        /// <summary>
        /// Translates this <see cref="PointF"/> by the specified amount.
        /// </summary>
        /// <param name="dx">The amount to offset the x-coordinate.</param>
        /// <param name="dy">The amount to offset the y-coordinate.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Offset(float dx, float dy)
        {
            this.X += dx;
            this.Y += dy;
        }

        /// <summary>
        /// Translates this <see cref="PointF"/> by the specified amount.
        /// </summary>
        /// <param name="point">The <see cref="PointF"/> used offset this <see cref="PointF"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Offset(PointF point) => this.Offset(point.X, point.Y);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.X, this.Y);

        /// <inheritdoc/>
        public override string ToString() => $"PointF [ X={this.X}, Y={this.Y} ]";

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is PointF && this.Equals((PointF)obj);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(PointF other) => this.X.Equals(other.X) && this.Y.Equals(other.Y);
    }
}
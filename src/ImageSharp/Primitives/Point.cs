// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp;

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
    public static readonly Point Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/> struct.
    /// </summary>
    /// <param name="value">The horizontal and vertical position of the point.</param>
    public Point(int value)
        : this()
    {
        this.X = LowInt16(value);
        this.Y = HighInt16(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/> struct.
    /// </summary>
    /// <param name="x">The horizontal position of the point.</param>
    /// <param name="y">The vertical position of the point.</param>
    public Point(int x, int y)
        : this()
    {
        this.X = x;
        this.Y = y;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/> struct from the given <see cref="Size"/>.
    /// </summary>
    /// <param name="size">The size.</param>
    public Point(Size size)
    {
        this.X = size.Width;
        this.Y = size.Height;
    }

    /// <summary>
    /// Gets or sets the x-coordinate of this <see cref="Point"/>.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the y-coordinate of this <see cref="Point"/>.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets a value indicating whether this <see cref="Point"/> is empty.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly bool IsEmpty => this.Equals(Empty);

    /// <summary>
    /// Creates a <see cref="PointF"/> with the coordinates of the specified <see cref="Point"/>.
    /// </summary>
    /// <param name="point">The point.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PointF(Point point) => new(point.X, point.Y);

    /// <summary>
    /// Creates a <see cref="Vector2"/> with the coordinates of the specified <see cref="Point"/>.
    /// </summary>
    /// <param name="point">The point.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector2(Point point) => new(point.X, point.Y);

    /// <summary>
    /// Creates a <see cref="Size"/> with the coordinates of the specified <see cref="Point"/>.
    /// </summary>
    /// <param name="point">The point.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Size(Point point) => new(point.X, point.Y);

    /// <summary>
    /// Negates the given point by multiplying all values by -1.
    /// </summary>
    /// <param name="value">The source point.</param>
    /// <returns>The negated point.</returns>
    public static Point operator -(Point value) => new(-value.X, -value.Y);

    /// <summary>
    /// Translates a <see cref="Point"/> by a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>
    /// The <see cref="Point"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point operator +(Point point, Size size) => Add(point, size);

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point operator -(Point point, Size size) => Subtract(point, size);

    /// <summary>
    /// Multiplies <see cref="Point"/> by a <see cref="int"/> producing <see cref="Point"/>.
    /// </summary>
    /// <param name="left">Multiplier of type <see cref="int"/>.</param>
    /// <param name="right">Multiplicand of type <see cref="Point"/>.</param>
    /// <returns>Product of type <see cref="Point"/>.</returns>
    public static Point operator *(int left, Point right) => Multiply(right, left);

    /// <summary>
    /// Multiplies <see cref="Point"/> by a <see cref="int"/> producing <see cref="Point"/>.
    /// </summary>
    /// <param name="left">Multiplicand of type <see cref="Point"/>.</param>
    /// <param name="right">Multiplier of type <see cref="int"/>.</param>
    /// <returns>Product of type <see cref="Point"/>.</returns>
    public static Point operator *(Point left, int right) => Multiply(left, right);

    /// <summary>
    /// Divides <see cref="Point"/> by a <see cref="int"/> producing <see cref="Point"/>.
    /// </summary>
    /// <param name="left">Dividend of type <see cref="Point"/>.</param>
    /// <param name="right">Divisor of type <see cref="int"/>.</param>
    /// <returns>Result of type <see cref="Point"/>.</returns>
    public static Point operator /(Point left, int right)
        => new(left.X / right, left.Y / right);

    /// <summary>
    /// Shift <see cref="Point"/> to the right by a <see cref="int"/> amount producing <see cref="Point"/>.
    /// </summary>
    /// <param name="left">Shifted value of type <see cref="Point"/>.</param>
    /// <param name="right">Shifted amount of type <see cref="int"/>.</param>
    /// <returns>Result of type <see cref="Point"/>.</returns>
    public static Point operator >>(Point left, int right)
        => new(left.X >> right, left.Y >> right);

    /// <summary>
    /// Shift <see cref="Point"/> to the left by a <see cref="int"/> amount producing <see cref="Point"/>.
    /// </summary>
    /// <param name="left">Shifted value of type <see cref="Point"/>.</param>
    /// <param name="right">Shifted amount of type <see cref="int"/>.</param>
    /// <returns>Result of type <see cref="Point"/>.</returns>
    public static Point operator <<(Point left, int right)
        => new(left.X << right, left.Y << right);

    /// <summary>
    /// Compares two <see cref="Point"/> objects for equality.
    /// </summary>
    /// <param name="left">The <see cref="Point"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Point"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Point left, Point right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Point"/> objects for inequality.
    /// </summary>
    /// <param name="left">The <see cref="Point"/> on the left side of the operand.</param>
    /// <param name="right">The <see cref="Point"/> on the right side of the operand.</param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Point left, Point right) => !left.Equals(right);

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Add(Point point, Size size) => new(unchecked(point.X + size.Width), unchecked(point.Y + size.Height));

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given value.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="value">The value on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Multiply(Point point, int value) => new(unchecked(point.X * value), unchecked(point.Y * value));

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Subtract(Point point, Size size) => new(unchecked(point.X - size.Width), unchecked(point.Y - size.Height));

    /// <summary>
    /// Converts a <see cref="PointF"/> to a <see cref="Point"/> by performing a ceiling operation on all the coordinates.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Ceiling(PointF point) => new(unchecked((int)MathF.Ceiling(point.X)), unchecked((int)MathF.Ceiling(point.Y)));

    /// <summary>
    /// Converts a <see cref="PointF"/> to a <see cref="Point"/> by performing a round operation on all the coordinates.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Round(PointF point) => new(unchecked((int)MathF.Round(point.X)), unchecked((int)MathF.Round(point.Y)));

    /// <summary>
    /// Converts a <see cref="Vector2"/> to a <see cref="Point"/> by performing a round operation on all the coordinates.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Round(Vector2 vector) => new(unchecked((int)MathF.Round(vector.X)), unchecked((int)MathF.Round(vector.Y)));

    /// <summary>
    /// Converts a <see cref="PointF"/> to a <see cref="Point"/> by performing a truncate operation on all the coordinates.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Truncate(PointF point) => new(unchecked((int)point.X), unchecked((int)point.Y));

    /// <summary>
    /// Transforms a point by a specified 3x2 matrix.
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="matrix">The transformation matrix used.</param>
    /// <returns>The transformed <see cref="PointF"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Transform(Point point, Matrix3x2 matrix) => Round(Vector2.Transform(new Vector2(point.X, point.Y), matrix));

    /// <summary>
    /// Deconstructs this point into two integers.
    /// </summary>
    /// <param name="x">The out value for X.</param>
    /// <param name="y">The out value for Y.</param>
    public readonly void Deconstruct(out int x, out int y)
    {
        x = this.X;
        y = this.Y;
    }

    /// <summary>
    /// Translates this <see cref="Point"/> by the specified amount.
    /// </summary>
    /// <param name="dx">The amount to offset the x-coordinate.</param>
    /// <param name="dy">The amount to offset the y-coordinate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Offset(int dx, int dy)
    {
        unchecked
        {
            this.X += dx;
            this.Y += dy;
        }
    }

    /// <summary>
    /// Translates this <see cref="Point"/> by the specified amount.
    /// </summary>
    /// <param name="point">The <see cref="Point"/> used offset this <see cref="Point"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Offset(Point point) => this.Offset(point.X, point.Y);

    /// <summary>
    /// Shifts the coordinate value of this <see cref="Point"/> to the right with the specified amount.
    /// </summary>
    /// <param name="point">The point to shift.</param>
    /// <param name="bitCount">The number of bits to shift to the right.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point ShiftRight(Point point, int bitCount) => new(unchecked(point.X >> bitCount), unchecked(point.Y >> bitCount));

    /// <summary>
    /// Shifts the coordinate value of this <see cref="Point"/> to the left with the specified amount.
    /// </summary>
    /// <param name="point">The point to shift.</param>
    /// <param name="bitCount">The number of bits to shift to the left.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point ShiftLeft(Point point, int bitCount) => new(unchecked(point.X << bitCount), unchecked(point.Y << bitCount));

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(this.X, this.Y);

    /// <inheritdoc/>
    public override readonly string ToString() => $"Point [ X={this.X}, Y={this.Y} ]";

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is Point other && this.Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(Point other) => this.X.Equals(other.X) && this.Y.Equals(other.Y);

    private static short HighInt16(int n) => unchecked((short)((n >> 16) & 0xffff));

    private static short LowInt16(int n) => unchecked((short)(n & 0xffff));
}

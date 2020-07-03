// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Stores an ordered pair of integers, which specify a height and width.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct Size : IEquatable<Size>
    {
        /// <summary>
        /// Represents a <see cref="Size"/> that has Width and Height values set to zero.
        /// </summary>
        public static readonly Size Empty = default;

        /// <summary>
        /// Initializes a new instance of the <see cref="Size"/> struct.
        /// </summary>
        /// <param name="value">The width and height of the size.</param>
        public Size(int value)
            : this()
        {
            this.Width = value;
            this.Height = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Size"/> struct.
        /// </summary>
        /// <param name="width">The width of the size.</param>
        /// <param name="height">The height of the size.</param>
        public Size(int width, int height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Size"/> struct.
        /// </summary>
        /// <param name="size">The size.</param>
        public Size(Size size)
            : this()
        {
            this.Width = size.Width;
            this.Height = size.Height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Size"/> struct from the given <see cref="Point"/>.
        /// </summary>
        /// <param name="point">The point.</param>
        public Size(Point point)
        {
            this.Width = point.X;
            this.Height = point.Y;
        }

        /// <summary>
        /// Gets or sets the width of this <see cref="Size"/>.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the height of this <see cref="Size"/>.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Size"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Creates a <see cref="SizeF"/> with the dimensions of the specified <see cref="Size"/>.
        /// </summary>
        /// <param name="size">The point.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SizeF(Size size) => new SizeF(size.Width, size.Height);

        /// <summary>
        /// Converts the given <see cref="Size"/> into a <see cref="Point"/>.
        /// </summary>
        /// <param name="size">The size.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Point(Size size) => new Point(size.Width, size.Height);

        /// <summary>
        /// Computes the sum of adding two sizes.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size operator +(Size left, Size right) => Add(left, right);

        /// <summary>
        /// Computes the difference left by subtracting one size from another.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size operator -(Size left, Size right) => Subtract(left, right);

        /// <summary>
        /// Multiplies a <see cref="Size"/> by an <see cref="int"/> producing <see cref="Size"/>.
        /// </summary>
        /// <param name="left">Multiplier of type <see cref="int"/>.</param>
        /// <param name="right">Multiplicand of type <see cref="Size"/>.</param>
        /// <returns>Product of type <see cref="Size"/>.</returns>
        public static Size operator *(int left, Size right) => Multiply(right, left);

        /// <summary>
        /// Multiplies <see cref="Size"/> by an <see cref="int"/> producing <see cref="Size"/>.
        /// </summary>
        /// <param name="left">Multiplicand of type <see cref="Size"/>.</param>
        /// <param name="right">Multiplier of type <see cref="int"/>.</param>
        /// <returns>Product of type <see cref="Size"/>.</returns>
        public static Size operator *(Size left, int right) => Multiply(left, right);

        /// <summary>
        /// Divides <see cref="Size"/> by an <see cref="int"/> producing <see cref="Size"/>.
        /// </summary>
        /// <param name="left">Dividend of type <see cref="Size"/>.</param>
        /// <param name="right">Divisor of type <see cref="int"/>.</param>
        /// <returns>Result of type <see cref="Size"/>.</returns>
        public static Size operator /(Size left, int right) => new Size(unchecked(left.Width / right), unchecked(left.Height / right));

        /// <summary>
        /// Multiplies <see cref="Size"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Multiplier of type <see cref="float"/>.</param>
        /// <param name="right">Multiplicand of type <see cref="Size"/>.</param>
        /// <returns>Product of type <see cref="SizeF"/>.</returns>
        public static SizeF operator *(float left, Size right) => Multiply(right, left);

        /// <summary>
        /// Multiplies <see cref="Size"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Multiplicand of type <see cref="Size"/>.</param>
        /// <param name="right">Multiplier of type <see cref="float"/>.</param>
        /// <returns>Product of type <see cref="SizeF"/>.</returns>
        public static SizeF operator *(Size left, float right) => Multiply(left, right);

        /// <summary>
        /// Divides <see cref="Size"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Dividend of type <see cref="Size"/>.</param>
        /// <param name="right">Divisor of type <see cref="int"/>.</param>
        /// <returns>Result of type <see cref="SizeF"/>.</returns>
        public static SizeF operator /(Size left, float right)
            => new SizeF(left.Width / right, left.Height / right);

        /// <summary>
        /// Compares two <see cref="Size"/> objects for equality.
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Size left, Size right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Size"/> objects for inequality.
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Size left, Size right) => !left.Equals(right);

        /// <summary>
        /// Performs vector addition of two <see cref="Size"/> objects.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="Size"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size Add(Size left, Size right) => new Size(unchecked(left.Width + right.Width), unchecked(left.Height + right.Height));

        /// <summary>
        /// Contracts a <see cref="Size"/> by another <see cref="Size"/>.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="Size"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size Subtract(Size left, Size right) => new Size(unchecked(left.Width - right.Width), unchecked(left.Height - right.Height));

        /// <summary>
        /// Converts a <see cref="SizeF"/> to a <see cref="Size"/> by performing a ceiling operation on all the dimensions.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The <see cref="Size"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size Ceiling(SizeF size) => new Size(unchecked((int)MathF.Ceiling(size.Width)), unchecked((int)MathF.Ceiling(size.Height)));

        /// <summary>
        /// Converts a <see cref="SizeF"/> to a <see cref="Size"/> by performing a round operation on all the dimensions.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The <see cref="Size"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size Round(SizeF size) => new Size(unchecked((int)MathF.Round(size.Width)), unchecked((int)MathF.Round(size.Height)));

        /// <summary>
        /// Transforms a size by the given matrix.
        /// </summary>
        /// <param name="size">The source size.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>A transformed size.</returns>
        public static SizeF Transform(Size size, Matrix3x2 matrix)
        {
            var v = Vector2.Transform(new Vector2(size.Width, size.Height), matrix);

            return new SizeF(v.X, v.Y);
        }

        /// <summary>
        /// Converts a <see cref="SizeF"/> to a <see cref="Size"/> by performing a round operation on all the dimensions.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The <see cref="Size"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size Truncate(SizeF size) => new Size(unchecked((int)size.Width), unchecked((int)size.Height));

        /// <summary>
        /// Deconstructs this size into two integers.
        /// </summary>
        /// <param name="width">The out value for the width.</param>
        /// <param name="height">The out value for the height.</param>
        public void Deconstruct(out int width, out int height)
        {
            width = this.Width;
            height = this.Height;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Width, this.Height);

        /// <inheritdoc/>
        public override string ToString() => $"Size [ Width={this.Width}, Height={this.Height} ]";

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Size other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Size other) => this.Width.Equals(other.Width) && this.Height.Equals(other.Height);

        /// <summary>
        /// Multiplies <see cref="Size"/> by an <see cref="int"/> producing <see cref="Size"/>.
        /// </summary>
        /// <param name="size">Multiplicand of type <see cref="Size"/>.</param>
        /// <param name="multiplier">Multiplier of type <see cref="int"/>.</param>
        /// <returns>Product of type <see cref="Size"/>.</returns>
        private static Size Multiply(Size size, int multiplier) =>
            new Size(unchecked(size.Width * multiplier), unchecked(size.Height * multiplier));

        /// <summary>
        /// Multiplies <see cref="Size"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="size">Multiplicand of type <see cref="Size"/>.</param>
        /// <param name="multiplier">Multiplier of type <see cref="float"/>.</param>
        /// <returns>Product of type SizeF.</returns>
        private static SizeF Multiply(Size size, float multiplier) =>
            new SizeF(size.Width * multiplier, size.Height * multiplier);
    }
}
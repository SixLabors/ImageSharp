// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Stores an ordered pair of single precision floating points, which specify a height and width.
    /// </summary>
    /// <remarks>
    /// This struct is fully mutable. This is done (against the guidelines) for the sake of performance,
    /// as it avoids the need to create new values for modification operations.
    /// </remarks>
    public struct SizeF : IEquatable<SizeF>
    {
        /// <summary>
        /// Represents a <see cref="SizeF"/> that has Width and Height values set to zero.
        /// </summary>
        public static readonly SizeF Empty = default;

        /// <summary>
        /// Initializes a new instance of the <see cref="SizeF"/> struct.
        /// </summary>
        /// <param name="width">The width of the size.</param>
        /// <param name="height">The height of the size.</param>
        public SizeF(float width, float height)
        {
            this.Width = width;
            this.Height = height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SizeF"/> struct.
        /// </summary>
        /// <param name="size">The size.</param>
        public SizeF(SizeF size)
            : this()
        {
            this.Width = size.Width;
            this.Height = size.Height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SizeF"/> struct from the given <see cref="PointF"/>.
        /// </summary>
        /// <param name="point">The point.</param>
        public SizeF(PointF point)
        {
            this.Width = point.X;
            this.Height = point.Y;
        }

        /// <summary>
        /// Gets or sets the width of this <see cref="SizeF"/>.
        /// </summary>
        public float Width { get; set; }

        /// <summary>
        /// Gets or sets the height of this <see cref="SizeF"/>.
        /// </summary>
        public float Height { get; set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="SizeF"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Creates a <see cref="Vector2"/> with the coordinates of the specified <see cref="PointF"/>.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The <see cref="Vector2"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector2(SizeF point) => new Vector2(point.Width, point.Height);

        /// <summary>
        /// Creates a <see cref="Size"/> with the dimensions of the specified <see cref="SizeF"/> by truncating each of the dimensions.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Size(SizeF size) => new Size(unchecked((int)size.Width), unchecked((int)size.Height));

        /// <summary>
        /// Converts the given <see cref="SizeF"/> into a <see cref="PointF"/>.
        /// </summary>
        /// <param name="size">The size.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator PointF(SizeF size) => new PointF(size.Width, size.Height);

        /// <summary>
        /// Computes the sum of adding two sizes.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="SizeF"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SizeF operator +(SizeF left, SizeF right) => Add(left, right);

        /// <summary>
        /// Computes the difference left by subtracting one size from another.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="SizeF"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SizeF operator -(SizeF left, SizeF right) => Subtract(left, right);

        /// <summary>
        /// Multiplies <see cref="SizeF"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Multiplier of type <see cref="float"/>.</param>
        /// <param name="right">Multiplicand of type <see cref="SizeF"/>.</param>
        /// <returns>Product of type <see cref="SizeF"/>.</returns>
        public static SizeF operator *(float left, SizeF right) => Multiply(right, left);

        /// <summary>
        /// Multiplies <see cref="SizeF"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Multiplicand of type <see cref="SizeF"/>.</param>
        /// <param name="right">Multiplier of type <see cref="float"/>.</param>
        /// <returns>Product of type <see cref="SizeF"/>.</returns>
        public static SizeF operator *(SizeF left, float right) => Multiply(left, right);

        /// <summary>
        /// Divides <see cref="SizeF"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">Dividend of type <see cref="SizeF"/>.</param>
        /// <param name="right">Divisor of type <see cref="int"/>.</param>
        /// <returns>Result of type <see cref="SizeF"/>.</returns>
        public static SizeF operator /(SizeF left, float right)
            => new SizeF(left.Width / right, left.Height / right);

        /// <summary>
        /// Compares two <see cref="SizeF"/> objects for equality.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SizeF left, SizeF right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="SizeF"/> objects for inequality.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SizeF left, SizeF right) => !left.Equals(right);

        /// <summary>
        /// Performs vector addition of two <see cref="SizeF"/> objects.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="SizeF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SizeF Add(SizeF left, SizeF right) => new SizeF(left.Width + right.Width, left.Height + right.Height);

        /// <summary>
        /// Contracts a <see cref="SizeF"/> by another <see cref="SizeF"/>.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="SizeF"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SizeF Subtract(SizeF left, SizeF right) => new SizeF(left.Width - right.Width, left.Height - right.Height);

        /// <summary>
        /// Transforms a size by the given matrix.
        /// </summary>
        /// <param name="size">The source size.</param>
        /// <param name="matrix">The transformation matrix.</param>
        /// <returns>A transformed size.</returns>
        public static SizeF Transform(SizeF size, Matrix3x2 matrix)
        {
            var v = Vector2.Transform(new Vector2(size.Width, size.Height), matrix);

            return new SizeF(v.X, v.Y);
        }

        /// <summary>
        /// Deconstructs this size into two floats.
        /// </summary>
        /// <param name="width">The out value for the width.</param>
        /// <param name="height">The out value for the height.</param>
        public void Deconstruct(out float width, out float height)
        {
            width = this.Width;
            height = this.Height;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.Width, this.Height);

        /// <inheritdoc/>
        public override string ToString() => $"SizeF [ Width={this.Width}, Height={this.Height} ]";

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SizeF && this.Equals((SizeF)obj);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SizeF other) => this.Width.Equals(other.Width) && this.Height.Equals(other.Height);

        /// <summary>
        /// Multiplies <see cref="SizeF"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
        /// </summary>
        /// <param name="size">Multiplicand of type <see cref="SizeF"/>.</param>
        /// <param name="multiplier">Multiplier of type <see cref="float"/>.</param>
        /// <returns>Product of type SizeF.</returns>
        private static SizeF Multiply(SizeF size, float multiplier) =>
            new SizeF(size.Width * multiplier, size.Height * multiplier);
    }
}
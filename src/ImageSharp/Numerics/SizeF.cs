// <copyright file="SizeF.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

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
        public static readonly SizeF Empty = default(SizeF);

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
        /// <param name="size">The size</param>
        public SizeF(SizeF size)
            : this()
        {
            this.Width = size.Width;
            this.Height = size.Height;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SizeF"/> struct from the given <see cref="PointF"/>.
        /// </summary>
        /// <param name="point">The point</param>
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
        /// Creates a <see cref="Size"/> with the dimensions of the specified <see cref="SizeF"/> by truncating each of the dimensions.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>
        /// The <see cref="Size"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Size(SizeF size)
        {
            return new Size(unchecked((int)size.Width), unchecked((int)size.Height));
        }

        /// <summary>
        /// Converts the given <see cref="SizeF"/> into a <see cref="PointF"/>.
        /// </summary>
        /// <param name="size">The size</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator PointF(SizeF size)
        {
            return new PointF(size.Width, size.Height);
        }

        /// <summary>
        /// Computes the sum of adding two sizes.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="SizeF"/>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SizeF operator +(SizeF left, SizeF right)
        {
            return Add(left, right);
        }

        /// <summary>
        /// Computes the difference left by subtracting one size from another.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="SizeF"/>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SizeF operator -(SizeF left, SizeF right)
        {
            return Subtract(left, right);
        }

        /// <summary>
        /// Compares two <see cref="SizeF"/> objects for equality.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SizeF left, SizeF right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="SizeF"/> objects for inequality.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SizeF left, SizeF right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Performs vector addition of two <see cref="SizeF"/> objects.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="SizeF"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SizeF Add(SizeF left, SizeF right)
        {
            return new SizeF(left.Width + right.Width, left.Height + right.Height);
        }

        /// <summary>
        /// Contracts a <see cref="SizeF"/> by another <see cref="SizeF"/>
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>The <see cref="SizeF"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SizeF Subtract(SizeF left, SizeF right)
        {
            return new SizeF(left.Width - right.Width, left.Height - right.Height);
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
                return "SizeF [ Empty ]";
            }

            return $"SizeF [ Width={this.Width}, Height={this.Height} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is SizeF)
            {
                return this.Equals((SizeF)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SizeF other)
        {
            return this.Width.Equals(other.Width) && this.Height.Equals(other.Height);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="size">
        /// The instance of <see cref="SizeF"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(SizeF size)
        {
            unchecked
            {
                return size.Width.GetHashCode() ^ size.Height.GetHashCode();
            }
        }
    }
}
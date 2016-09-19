// <copyright file="Size.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

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
        public static readonly Size Empty = default(Size);

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
        /// Computes the sum of adding two sizes.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Size"/>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size operator +(Size left, Size right)
        {
            return new Size(left.Width + right.Width, left.Height + right.Height);
        }

        /// <summary>
        /// Computes the difference left by subtracting one size from another.
        /// </summary>
        /// <param name="left">The size on the left hand of the operand.</param>
        /// <param name="right">The size on the right hand of the operand.</param>
        /// <returns>
        /// The <see cref="Size"/>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size operator -(Size left, Size right)
        {
            return new Size(left.Width - right.Width, left.Height - right.Height);
        }

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
        public static bool operator ==(Size left, Size right)
        {
            return left.Equals(right);
        }

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
        public static bool operator !=(Size left, Size right)
        {
            return !left.Equals(right);
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
                return "Size [ Empty ]";
            }

            return $"Size [ Width={this.Width}, Height={this.Height} ]";
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is Size)
            {
                return this.Equals((Size)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(Size other)
        {
            return this.Width == other.Width && this.Height == other.Height;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="size">
        /// The instance of <see cref="Size"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Size size)
        {
            return size.Width ^ size.Height;
        }
    }
}

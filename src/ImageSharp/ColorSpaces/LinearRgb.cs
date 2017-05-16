// <copyright file="LinearRgb.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.ColorSpaces
{
    using System;
    using System.ComponentModel;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents an linear Rgb color with specified <see cref="IRgbWorkingSpace"/> working space
    /// </summary>
    internal struct LinearRgb : IColorVector, IEquatable<LinearRgb>, IAlmostEquatable<LinearRgb, float>
    {
        /// <summary>
        /// Represents a <see cref="LinearRgb"/> that has R, G, and B values set to zero.
        /// </summary>
        public static readonly LinearRgb Empty = default(LinearRgb);

        /// <summary>
        /// The default LinearRgb working space
        /// </summary>
        public static readonly IRgbWorkingSpace DefaultWorkingSpace = RgbWorkingSpaces.SRgb;

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearRgb"/> struct.
        /// </summary>
        /// <param name="r">The red component ranging between 0 and 1.</param>
        /// <param name="g">The green component ranging between 0 and 1.</param>
        /// <param name="b">The blue component ranging between 0 and 1.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LinearRgb(float r, float g, float b)
            : this(new Vector3(r, g, b))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearRgb"/> struct.
        /// </summary>
        /// <param name="r">The red component ranging between 0 and 1.</param>
        /// <param name="g">The green component ranging between 0 and 1.</param>
        /// <param name="b">The blue component ranging between 0 and 1.</param>
        /// <param name="workingSpace">The rgb working space.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LinearRgb(float r, float g, float b, IRgbWorkingSpace workingSpace)
            : this(new Vector3(r, g, b), workingSpace)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearRgb"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the r, g, b components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LinearRgb(Vector3 vector)
            : this(vector, DefaultWorkingSpace)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinearRgb"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the r, g, b components.</param>
        /// <param name="workingSpace">The LinearRgb working space.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LinearRgb(Vector3 vector, IRgbWorkingSpace workingSpace)
            : this()
        {
            // Clamp to 0-1 range.
            this.backingVector = Vector3.Clamp(vector, Vector3.Zero, Vector3.One);
            this.WorkingSpace = workingSpace;
        }

        /// <summary>
        /// Gets the red component.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float R
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the green component.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float G
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

        /// <summary>
        /// Gets the blue component.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float B
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <summary>
        /// Gets the LinearRgb color space <seealso cref="RgbWorkingSpaces"/>
        /// </summary>
        public IRgbWorkingSpace WorkingSpace { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="LinearRgb"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <inheritdoc />
        public Vector3 Vector
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector;
        }

        /// <summary>
        /// Compares two <see cref="LinearRgb"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="LinearRgb"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="LinearRgb"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(LinearRgb left, LinearRgb right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="LinearRgb"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="LinearRgb"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="LinearRgb"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(LinearRgb left, LinearRgb right)
        {
            return !left.Equals(right);
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
                return "LinearRgb [ Empty ]";
            }

            return $"LinearRgb [ R={this.R:#0.##}, G={this.G:#0.##}, B={this.B:#0.##} ]";
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is LinearRgb)
            {
                return this.Equals((LinearRgb)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(LinearRgb other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(LinearRgb other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}
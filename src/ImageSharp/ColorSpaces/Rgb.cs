// <copyright file="Rgb.cs" company="James Jackson-South">
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
    /// Represents an RGB color with specified <see cref="IRgbWorkingSpace"/> working space
    /// </summary>
    internal struct Rgb : IColorVector, IEquatable<Rgb>, IAlmostEquatable<Rgb, float>
    {
        /// <summary>
        /// Represents a <see cref="Rgb"/> that has R, G, and B values set to zero.
        /// </summary>
        public static readonly Rgb Empty = default(Rgb);

        /// <summary>
        /// The default rgb working space
        /// </summary>
        public static readonly IRgbWorkingSpace DefaultWorkingSpace = RgbWorkingSpaces.SRgb;

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb"/> struct.
        /// </summary>
        /// <param name="r">The red component ranging between 0 and 1.</param>
        /// <param name="g">The green component ranging between 0 and 1.</param>
        /// <param name="b">The blue component ranging between 0 and 1.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb(float r, float g, float b)
            : this(new Vector3(r, g, b))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb"/> struct.
        /// </summary>
        /// <param name="r">The red component ranging between 0 and 1.</param>
        /// <param name="g">The green component ranging between 0 and 1.</param>
        /// <param name="b">The blue component ranging between 0 and 1.</param>
        /// <param name="workingSpace">The rgb working space.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb(float r, float g, float b, IRgbWorkingSpace workingSpace)
            : this(new Vector3(r, g, b), workingSpace)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the r, g, b components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb(Vector3 vector)
            : this(vector, DefaultWorkingSpace)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgb"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the r, g, b components.</param>
        /// <param name="workingSpace">The rgb working space.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rgb(Vector3 vector, IRgbWorkingSpace workingSpace)
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
        /// Gets the Rgb color space <seealso cref="RgbWorkingSpaces"/>
        /// </summary>
        public IRgbWorkingSpace WorkingSpace
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Rgb"/> is empty.
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
        /// Allows the implicit conversion of an instance of <see cref="Rgba32"/> to a
        /// <see cref="Rgb"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Rgba32"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Rgb"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Rgb(Rgba32 color)
        {
            return new Rgb(color.R / 255F, color.G / 255F, color.B / 255F);
        }

        /// <summary>
        /// Compares two <see cref="Rgb"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgb"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgb"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Rgb left, Rgb right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Rgb"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Rgb"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Rgb"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Rgb left, Rgb right)
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
                return "Rgb [ Empty ]";
            }

            return $"Rgb [ R={this.R:#0.##}, G={this.G:#0.##}, B={this.B:#0.##} ]";
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is Rgb)
            {
                return this.Equals((Rgb)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Rgb other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(Rgb other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}
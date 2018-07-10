// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.RgbColorSapce;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents an RGB color with specified <see cref="RgbWorkingSpace"/> working space
    /// </summary>
    internal readonly struct Rgb : IColorVector, IEquatable<Rgb>, IAlmostEquatable<Rgb, float>
    {
        /// <summary>
        /// The default rgb working space
        /// </summary>
        public static readonly RgbWorkingSpace DefaultWorkingSpace = RgbWorkingSpaces.SRgb;

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
        public Rgb(float r, float g, float b, RgbWorkingSpace workingSpace)
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
        public Rgb(Vector3 vector, RgbWorkingSpace workingSpace)
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
        public RgbWorkingSpace WorkingSpace { get; }

        /// <inheritdoc />
        public Vector3 Vector => this.backingVector;

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
            return this.Equals(default)
                ? "Rgb [ Empty ]"
                : $"Rgb [ R={this.R:#0.##}, G={this.G:#0.##}, B={this.B:#0.##} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is Rgb other && this.Equals(other);
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
// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents an CIE xyY 1931 color
    /// <see href="https://en.wikipedia.org/wiki/CIE_1931_color_space#CIE_xy_chromaticity_diagram_and_the_CIE_xyY_color_space"/>
    /// </summary>
    internal readonly struct CieXyy : IColorVector, IEquatable<CieXyy>, IAlmostEquatable<CieXyy, float>
    {
        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyy"/> struct.
        /// </summary>
        /// <param name="x">The x chroma component.</param>
        /// <param name="y">The y chroma component.</param>
        /// <param name="yl">The y luminance component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyy(float x, float y, float yl)
            : this(new Vector3(x, y, yl))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyy"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the x, y, Y components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyy(Vector3 vector)
            : this()
        {
            // Not clamping as documentation about this space seems to indicate "usual" ranges
            this.backingVector = vector;
        }

        /// <summary>
        /// Gets the X chrominance component.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float X
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the Y chrominance component.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float Y
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float Yl
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <inheritdoc />
        public Vector3 Vector => this.backingVector;

        /// <summary>
        /// Compares two <see cref="CieXyy"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieXyy"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieXyy"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CieXyy left, CieXyy right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieXyy"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieXyy"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieXyy"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CieXyy left, CieXyy right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return this.backingVector.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Equals(default)
                ? "CieXyy [ Empty ]"
                : $"CieXyy [ X={this.X:#0.##}, Y={this.Y:#0.##}, Yl={this.Yl:#0.##} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CieXyy other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CieXyy other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(CieXyy other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}
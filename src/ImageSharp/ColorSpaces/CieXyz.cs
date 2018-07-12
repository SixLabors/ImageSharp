// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents an CIE XYZ 1931 color
    /// <see href="https://en.wikipedia.org/wiki/CIE_1931_color_space#Definition_of_the_CIE_XYZ_color_space"/>
    /// </summary>
    internal readonly struct CieXyz : IColorVector, IEquatable<CieXyz>, IAlmostEquatable<CieXyz, float>
    {
        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyz"/> struct.
        /// </summary>
        /// <param name="x">X is a mix (a linear combination) of cone response curves chosen to be nonnegative</param>
        /// <param name="y">The y luminance component.</param>
        /// <param name="z">Z is quasi-equal to blue stimulation, or the S cone of the human eye.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CieXyz(float x, float y, float z)
            : this(new Vector3(x, y, z))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyz"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the x, y, z components.</param>
        public CieXyz(Vector3 vector)
            : this()
        {
            // Not clamping as documentation about this space seems to indicate "usual" ranges
            this.backingVector = vector;
        }

        /// <summary>
        /// Gets the X component. A mix (a linear combination) of cone response curves chosen to be nonnegative.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float X
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float Y
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

        /// <summary>
        /// Gets the Z component. Quasi-equal to blue stimulation, or the S cone response
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float Z
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <inheritdoc />
        public Vector3 Vector => this.backingVector;

        /// <summary>
        /// Compares two <see cref="CieXyz"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieXyz"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieXyz"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CieXyz left, CieXyz right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CieXyz"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="CieXyz"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="CieXyz"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CieXyz left, CieXyz right)
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
                ? "CieXyz [ Empty ]"
                : $"CieXyz [ X={this.X:#0.##}, Y={this.Y:#0.##}, Z={this.Z:#0.##} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CieXyz other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CieXyz other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(CieXyz other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}
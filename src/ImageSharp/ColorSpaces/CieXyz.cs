// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents an CIE XYZ 1931 color
    /// <see href="https://en.wikipedia.org/wiki/CIE_1931_color_space#Definition_of_the_CIE_XYZ_color_space"/>
    /// </summary>
    internal readonly struct CieXyz : IEquatable<CieXyz>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyz"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the x, y, z components.</param>
        /// <remarks>This colorspace is not clamped.</remarks>
        public CieXyz(Vector3 vector)
            : this(vector.X, vector.Y, vector.Z)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CieXyz"/> struct.
        /// </summary>
        /// <param name="x">X is a mix (a linear combination) of cone response curves chosen to be nonnegative</param>
        /// <param name="y">The y luminance component.</param>
        /// <param name="z">Z is quasi-equal to blue stimulation, or the S cone of the human eye.</param>
        public CieXyz(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        /// <summary>
        /// Gets the X component. A mix (a linear combination) of cone response curves chosen to be nonnegative.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float X { get; }

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float Y { get; }

        /// <summary>
        /// Gets the Z component. Quasi-equal to blue stimulation, or the S cone response
        /// <remarks>A value usually ranging between 0 and 1.</remarks>
        /// </summary>
        public float Z { get; }

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

        /// <summary>
        /// Returns a new <see cref="Vector3"/> representing this instance.
        /// </summary>
        /// <returns>The <see cref="Vector3"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToVector3() => new Vector3(this.X, this.Y, this.Z);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => (this.X, this.Y, this.Z).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"CieXyz({this.X:#0.##},{this.Y:#0.##},{this.Z:#0.##})";

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CieXyz other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CieXyz other) =>
            this.X.Equals(other.X) &&
            this.Y.Equals(other.Y) &&
            this.Z.Equals(other.Z);
    }
}
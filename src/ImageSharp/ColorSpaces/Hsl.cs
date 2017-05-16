// <copyright file="Hsl.cs" company="James Jackson-South">
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
    /// Represents a Hsl (hue, saturation, lightness) color.
    /// </summary>
    internal struct Hsl : IColorVector, IEquatable<Hsl>, IAlmostEquatable<Hsl, float>
    {
        /// <summary>
        /// Represents a <see cref="Hsl"/> that has H, S, and L values set to zero.
        /// </summary>
        public static readonly Hsl Empty = default(Hsl);

        /// <summary>
        /// Max range used for clamping
        /// </summary>
        private static readonly Vector3 VectorMax = new Vector3(360, 1, 1);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="Hsl"/> struct.
        /// </summary>
        /// <param name="h">The h hue component.</param>
        /// <param name="s">The s saturation component.</param>
        /// <param name="l">The l value (lightness) component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Hsl(float h, float s, float l)
            : this(new Vector3(h, s, l))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Hsl"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the h, s, l components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Hsl(Vector3 vector)
        {
            this.backingVector = Vector3.Clamp(vector, Vector3.Zero, VectorMax);
        }

        /// <summary>
        /// Gets the hue component.
        /// <remarks>A value ranging between 0 and 360.</remarks>
        /// </summary>
        public float H
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the saturation component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float S
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

        /// <summary>
        /// Gets the lightness component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float L
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Hsl"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <inheritdoc/>
        public Vector3 Vector
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector;
        }

        /// <summary>
        /// Compares two <see cref="Hsl"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Hsl"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Hsl"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Hsl left, Hsl right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Hsl"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Hsl"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Hsl"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Hsl left, Hsl right)
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
                return "Hsl [ Empty ]";
            }

            return $"Hsl [ H={this.H:#0.##}, S={this.S:#0.##}, L={this.L:#0.##} ]";
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is Hsl)
            {
                return this.Equals((Hsl)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Hsl other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(Hsl other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}
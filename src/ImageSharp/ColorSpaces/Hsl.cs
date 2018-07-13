// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents a Hsl (hue, saturation, lightness) color.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Hsl : IEquatable<Hsl>
    {
        /// <summary>
        /// Max range used for clamping.
        /// </summary>
        private static readonly Vector3 VectorMax = new Vector3(360, 1, 1);

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
            vector = Vector3.Clamp(vector, Vector3.Zero, VectorMax);

            this.H = vector.X;
            this.S = vector.Y;
            this.L = vector.Z;
        }

        /// <summary>
        /// Gets the hue component.
        /// <remarks>A value ranging between 0 and 360.</remarks>
        /// </summary>
        public float H { get; }

        /// <summary>
        /// Gets the saturation component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float S { get; }

        /// <summary>
        /// Gets the lightness component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float L { get; }

        /// <summary>
        /// Compares two <see cref="Hsl"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Hsl"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Hsl"/> on the right side of the operand.</param>
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
        /// <param name="left">The <see cref="Hsl"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Hsl"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Hsl left, Hsl right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => (this.H, this.S, this.L).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"Hsl({this.H:#0.##},{this.S:#0.##},{this.L:#0.##})";

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is Hsl other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Hsl other) =>
            this.H == other.H &&
            this.S == other.S &&
            this.L == other.L;
    }
}
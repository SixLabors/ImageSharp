// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents a HSV (hue, saturation, value) color. Also known as HSB (hue, saturation, brightness).
    /// </summary>
    public readonly struct Hsv : IEquatable<Hsv>
    {
        private static readonly Vector3 Min = Vector3.Zero;
        private static readonly Vector3 Max = new Vector3(360, 1, 1);

        /// <summary>
        /// Gets the hue component.
        /// <remarks>A value ranging between 0 and 360.</remarks>
        /// </summary>
        public readonly float H;

        /// <summary>
        /// Gets the saturation component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public readonly float S;

        /// <summary>
        /// Gets the value (brightness) component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public readonly float V;

        /// <summary>
        /// Initializes a new instance of the <see cref="Hsv"/> struct.
        /// </summary>
        /// <param name="h">The h hue component.</param>
        /// <param name="s">The s saturation component.</param>
        /// <param name="v">The v value (brightness) component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Hsv(float h, float s, float v)
            : this(new Vector3(h, s, v))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Hsv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the h, s, v components.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Hsv(Vector3 vector)
        {
            vector = Vector3.Clamp(vector, Min, Max);
            this.H = vector.X;
            this.S = vector.Y;
            this.V = vector.Z;
        }

        /// <summary>
        /// Compares two <see cref="Hsv"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Hsv"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Hsv"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Hsv left, Hsv right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Hsv"/> objects for inequality.
        /// </summary>
        /// <param name="left">The <see cref="Hsv"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Hsv"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Hsv left, Hsv right) => !left.Equals(right);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => HashCode.Combine(this.H, this.S, this.V);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"Hsv({this.H:#0.##}, {this.S:#0.##}, {this.V:#0.##})");

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Hsv other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(Hsv other)
        {
            return this.H.Equals(other.H)
                && this.S.Equals(other.S)
                && this.V.Equals(other.V);
        }
    }
}
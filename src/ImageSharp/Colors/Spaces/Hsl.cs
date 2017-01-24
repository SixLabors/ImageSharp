// <copyright file="Hsl.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents a Hsl (hue, saturation, lightness) color.
    /// </summary>
    public struct Hsl : IEquatable<Hsl>, IAlmostEquatable<Hsl, float>
    {
        /// <summary>
        /// Represents a <see cref="Hsl"/> that has H, S, and L values set to zero.
        /// </summary>
        public static readonly Hsl Empty = default(Hsl);

        /// <summary>
        /// Min range used for clamping
        /// </summary>
        private static readonly Vector3 VectorMin = Vector3.Zero;

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
        public Hsl(float h, float s, float l)
        {
            this.backingVector = Vector3.Clamp(new Vector3(h, s, l), VectorMin, VectorMax);
        }

        /// <summary>
        /// Gets the hue component.
        /// <remarks>A value ranging between 0 and 360.</remarks>
        /// </summary>
        public float H => this.backingVector.X;

        /// <summary>
        /// Gets the saturation component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float S => this.backingVector.Y;

        /// <summary>
        /// Gets the lightness component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float L => this.backingVector.Z;

        /// <summary>
        /// Gets a value indicating whether this <see cref="Hsl"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Color"/> to a
        /// <see cref="Hsl"/>.
        /// </summary>
        /// <param name="color">The instance of <see cref="Color"/> to convert.</param>
        /// <returns>
        /// An instance of <see cref="Hsl"/>.
        /// </returns>
        public static implicit operator Hsl(Color color)
        {
            float r = color.R / 255F;
            float g = color.G / 255F;
            float b = color.B / 255F;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float chroma = max - min;
            float h = 0;
            float s = 0;
            float l = (max + min) / 2;

            if (Math.Abs(chroma) < Constants.Epsilon)
            {
                return new Hsl(0, s, l);
            }

            if (Math.Abs(r - max) < Constants.Epsilon)
            {
                h = (g - b) / chroma;
            }
            else if (Math.Abs(g - max) < Constants.Epsilon)
            {
                h = 2 + ((b - r) / chroma);
            }
            else if (Math.Abs(b - max) < Constants.Epsilon)
            {
                h = 4 + ((r - g) / chroma);
            }

            h *= 60;
            if (h < 0.0)
            {
                h += 360;
            }

            if (l <= .5f)
            {
                s = chroma / (max + min);
            }
            else
            {
                s = chroma / (2 - chroma);
            }

            return new Hsl(h, s, l);
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
        public override bool Equals(object obj)
        {
            if (obj is Hsl)
            {
                return this.Equals((Hsl)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(Hsl other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        public bool AlmostEquals(Hsl other, float precision)
        {
            Vector3 result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}

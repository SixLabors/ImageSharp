// <copyright file="Hsv.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents a HSV (hue, saturation, value) color. Also known as HSB (hue, saturation, brightness).
    /// </summary>
    public struct Hsv : IEquatable<Hsv>, IAlmostEquatable<Hsv, float>
    {
        /// <summary>
        /// Represents a <see cref="Hsv"/> that has H, S, and V values set to zero.
        /// </summary>
        public static readonly Hsv Empty = default(Hsv);

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
        /// Initializes a new instance of the <see cref="Hsv"/> struct.
        /// </summary>
        /// <param name="h">The h hue component.</param>
        /// <param name="s">The s saturation component.</param>
        /// <param name="v">The v value (brightness) component.</param>
        public Hsv(float h, float s, float v)
        {
            this.backingVector = Vector3.Clamp(new Vector3(h, s, v), VectorMin, VectorMax);
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
        /// Gets the value (brightness) component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float V => this.backingVector.Z;

        /// <summary>
        /// Gets a value indicating whether this <see cref="Hsv"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Color"/> to a
        /// <see cref="Hsv"/>.
        /// </summary>
        /// <param name="color">The instance of <see cref="Color"/> to convert.</param>
        /// <returns>
        /// An instance of <see cref="Hsv"/>.
        /// </returns>
        public static implicit operator Hsv(Color color)
        {
            float r = color.R / 255F;
            float g = color.G / 255F;
            float b = color.B / 255F;

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float chroma = max - min;
            float h = 0;
            float s = 0;
            float v = max;

            if (Math.Abs(chroma) < Constants.Epsilon)
            {
                return new Hsv(0, s, v);
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

            s = chroma / v;

            return new Hsv(h, s, v);
        }

        /// <summary>
        /// Compares two <see cref="Hsv"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Hsv"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Hsv"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Hsv left, Hsv right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Hsv"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Hsv"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Hsv"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Hsv left, Hsv right)
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
                return "Hsv [ Empty ]";
            }

            return $"Hsv [ H={this.H:#0.##}, S={this.S:#0.##}, V={this.V:#0.##} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is Hsv)
            {
                return this.Equals((Hsv)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(Hsv other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        public bool AlmostEquals(Hsv other, float precision)
        {
            Vector3 result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}

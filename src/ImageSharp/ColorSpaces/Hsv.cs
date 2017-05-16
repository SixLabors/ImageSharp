// <copyright file="Hsv.cs" company="James Jackson-South">
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
    /// Represents a HSV (hue, saturation, value) color. Also known as HSB (hue, saturation, brightness).
    /// </summary>
    internal struct Hsv : IColorVector, IEquatable<Hsv>, IAlmostEquatable<Hsv, float>
    {
        /// <summary>
        /// Represents a <see cref="Hsv"/> that has H, S, and V values set to zero.
        /// </summary>
        public static readonly Hsv Empty = default(Hsv);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Hsv(float h, float s, float v)
            : this(new Vector3(h, s, v))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Hsv"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the h, s, v components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Hsv(Vector3 vector)
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
        /// Gets the value (brightness) component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float V
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Hsv"/> is empty.
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
        /// Allows the implicit conversion of an instance of <see cref="Rgba32"/> to a
        /// <see cref="Hsv"/>.
        /// </summary>
        /// <param name="color">The instance of <see cref="Rgba32"/> to convert.</param>
        /// <returns>
        /// An instance of <see cref="Hsv"/>.
        /// </returns>
        public static implicit operator Hsv(Rgba32 color)
        {
            float r = color.R / 255F;
            float g = color.G / 255F;
            float b = color.B / 255F;

            float max = MathF.Max(r, MathF.Max(g, b));
            float min = MathF.Min(r, MathF.Min(g, b));
            float chroma = max - min;
            float h = 0;
            float s = 0;
            float v = max;

            if (MathF.Abs(chroma) < Constants.Epsilon)
            {
                return new Hsv(0, s, v);
            }

            if (MathF.Abs(r - max) < Constants.Epsilon)
            {
                h = (g - b) / chroma;
            }
            else if (MathF.Abs(g - max) < Constants.Epsilon)
            {
                h = 2 + ((b - r) / chroma);
            }
            else if (MathF.Abs(b - max) < Constants.Epsilon)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is Hsv)
            {
                return this.Equals((Hsv)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Hsv other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(Hsv other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision;
        }
    }
}
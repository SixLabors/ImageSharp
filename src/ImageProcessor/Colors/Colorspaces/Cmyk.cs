// <copyright file="Cmyk.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;
    using System.Numerics;

    /// <summary>
    /// Represents an CMYK (cyan, magenta, yellow, keyline) color.
    /// </summary>
    public struct Cmyk : IEquatable<Cmyk>
    {
        /// <summary>
        /// Represents a <see cref="Cmyk"/> that has C, M, Y, and K values set to zero.
        /// </summary>
        public static readonly Cmyk Empty = default(Cmyk);

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private Vector4 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cmyk"/> struct.
        /// </summary>
        /// <param name="cyan">The cyan component.</param>
        /// <param name="magenta">The magenta component.</param>
        /// <param name="yellow">The yellow component.</param>
        /// <param name="keyline">The keyline black component.</param>
        public Cmyk(float cyan, float magenta, float yellow, float keyline)
            : this()
        {
            this.backingVector.X = Clamp(cyan);
            this.backingVector.Y = Clamp(magenta);
            this.backingVector.Z = Clamp(yellow);
            this.backingVector.W = Clamp(keyline);
        }

        /// <summary>
        /// Gets the cyan color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float C => this.backingVector.X;

        /// <summary>
        /// Gets the magenta color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float M => this.backingVector.Y;

        /// <summary>
        /// Gets the yellow color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float Y => this.backingVector.Z;

        /// <summary>
        /// Gets the keyline black color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float K => this.backingVector.W;

        /// <summary>
        /// Gets a value indicating whether this <see cref="Cmyk"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.backingVector.Equals(default(Vector4));

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Color"/> to a
        /// <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Bgra32"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Cmyk"/>.
        /// </returns>
        public static implicit operator Cmyk(Color color)
        {
            color = color.Limited;

            float c = 1f - color.R;
            float m = 1f - color.G;
            float y = 1f - color.B;

            float k = Math.Min(c, Math.Min(m, y));

            if (Math.Abs(k - 1.0f) <= Epsilon)
            {
                return new Cmyk(0, 0, 0, 1);
            }

            c = (c - k) / (1 - k);
            m = (m - k) / (1 - k);
            y = (y - k) / (1 - k);

            return new Cmyk(c, m, y, k);
        }

        /// <summary>
        /// Compares two <see cref="Cmyk"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Cmyk"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Cmyk"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(Cmyk left, Cmyk right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="Cmyk"/> objects for inequality
        /// </summary>
        /// <param name="left">
        /// The <see cref="Cmyk"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Cmyk"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator !=(Cmyk left, Cmyk right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is Cmyk)
            {
                Cmyk color = (Cmyk)obj;

                return this.backingVector == color.backingVector;
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "Cmyk [Empty]";
            }

            return $"Cmyk [ C={this.C:#0.##}, M={this.M:#0.##}, Y={this.Y:#0.##}, K={this.K:#0.##}]";
        }

        /// <inheritdoc/>
        public bool Equals(Cmyk other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <summary>
        /// Checks the range of the given value to ensure that it remains within the acceptable boundaries.
        /// </summary>
        /// <param name="value">
        /// The value to check.
        /// </param>
        /// <returns>
        /// The sanitized <see cref="float"/>.
        /// </returns>
        private static float Clamp(float value)
        {
            return value.Clamp(0, 1);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Cmyk"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private static int GetHashCode(Cmyk color) => color.backingVector.GetHashCode();
    }
}

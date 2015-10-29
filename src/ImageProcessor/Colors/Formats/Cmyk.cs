// <copyright file="Cmyk.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;
    using System.ComponentModel;

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
        /// Gets the cyan color component.
        /// </summary>
        /// <remarks>A value ranging between 0 and 100.</remarks>
        public readonly float C;

        /// <summary>
        /// Gets the magenta color component.
        /// </summary>
        /// <remarks>A value ranging between 0 and 100.</remarks>
        public readonly float M;

        /// <summary>
        /// Gets the yellow color component.
        /// </summary>
        /// <remarks>A value ranging between 0 and 100.</remarks>
        public readonly float Y;

        /// <summary>
        /// Gets the keyline black color component.
        /// </summary>
        /// <remarks>A value ranging between 0 and 100.</remarks>
        public readonly float K;

        /// <summary>
        /// The epsilon for comparing floating point numbers.
        /// </summary>
        private const float Epsilon = 0.0001f;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cmyk"/> struct.
        /// </summary>
        /// <param name="cyan">The cyan component.</param>
        /// <param name="magenta">The magenta component.</param>
        /// <param name="yellow">The yellow component.</param>
        /// <param name="keyline">The keyline black component.</param>
        public Cmyk(float cyan, float magenta, float yellow, float keyline)
        {
            this.C = Clamp(cyan);
            this.M = Clamp(magenta);
            this.Y = Clamp(yellow);
            this.K = Clamp(keyline);
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Cmyk"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => Math.Abs(this.C) < Epsilon
                            && Math.Abs(this.M) < Epsilon
                            && Math.Abs(this.Y) < Epsilon
                            && Math.Abs(this.K) < Epsilon;

        /// <summary>
        /// Allows the implicit conversion of an instance of <see cref="Bgra32"/> to a
        /// <see cref="Cmyk"/>.
        /// </summary>
        /// <param name="color">
        /// The instance of <see cref="Bgra32"/> to convert.
        /// </param>
        /// <returns>
        /// An instance of <see cref="Cmyk"/>.
        /// </returns>
        public static implicit operator Cmyk(Bgra32 color)
        {
            float c = (255f - color.R) / 255;
            float m = (255f - color.G) / 255;
            float y = (255f - color.B) / 255;

            float k = Math.Min(c, Math.Min(m, y));

            if (Math.Abs(k - 1.0) <= Epsilon)
            {
                return new Cmyk(0, 0, 0, 100);
            }

            c = ((c - k) / (1 - k)) * 100;
            m = ((m - k) / (1 - k)) * 100;
            y = ((y - k) / (1 - k)) * 100;

            return new Cmyk(c, m, y, k * 100);
        }

        /// <summary>
        /// Compares two <see cref="Cmyk"/> objects. The result specifies whether the values
        /// of the <see cref="Cmyk.C"/>, <see cref="Cmyk.M"/>, <see cref="Cmyk.Y"/>, and <see cref="Cmyk.K"/>
        /// properties of the two <see cref="Cmyk"/> objects are equal.
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
        /// Compares two <see cref="Cmyk"/> objects. The result specifies whether the values
        /// of the <see cref="Cmyk.C"/>, <see cref="Cmyk.M"/>, <see cref="Cmyk.Y"/>, and <see cref="Cmyk.K"/>
        /// properties of the two <see cref="Cmyk"/> objects are unequal.
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

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.
        /// </returns>
        /// <param name="obj">Another object to compare to. </param>
        public override bool Equals(object obj)
        {
            if (obj is Cmyk)
            {
                Cmyk color = (Cmyk)obj;

                return Math.Abs(this.C - color.C) < Epsilon
                    && Math.Abs(this.M - color.M) < Epsilon
                    && Math.Abs(this.Y - color.Y) < Epsilon
                    && Math.Abs(this.K - color.K) < Epsilon;
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.C.GetHashCode();
                hashCode = (hashCode * 397) ^ this.M.GetHashCode();
                hashCode = (hashCode * 397) ^ this.Y.GetHashCode();
                hashCode = (hashCode * 397) ^ this.K.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            if (this.IsEmpty)
            {
                return "Cmyk [Empty]";
            }

            return $"Cmyk [ C={this.C:#0.##}, M={this.M:#0.##}, Y={this.Y:#0.##}, K={this.K:#0.##}]";
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// True if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Cmyk other)
        {
            return Math.Abs(this.C - other.C) < Epsilon
                && Math.Abs(this.M - other.M) < Epsilon
                && Math.Abs(this.Y - other.Y) < Epsilon
                && Math.Abs(this.K - other.Y) < Epsilon;
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
            return value.Clamp(0, 100);
        }
    }
}

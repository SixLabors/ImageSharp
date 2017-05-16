// <copyright file="Cmyk.cs" company="James Jackson-South">
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
    /// Represents an CMYK (cyan, magenta, yellow, keyline) color.
    /// </summary>
    internal struct Cmyk : IEquatable<Cmyk>, IAlmostEquatable<Cmyk, float>
    {
        /// <summary>
        /// Represents a <see cref="Cmyk"/> that has C, M, Y, and K values set to zero.
        /// </summary>
        public static readonly Cmyk Empty = default(Cmyk);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector4 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cmyk"/> struct.
        /// </summary>
        /// <param name="c">The cyan component.</param>
        /// <param name="m">The magenta component.</param>
        /// <param name="y">The yellow component.</param>
        /// <param name="k">The keyline black component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Cmyk(float c, float m, float y, float k)
            : this(new Vector4(c, m, y, k))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cmyk"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the c, m, y, k components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Cmyk(Vector4 vector)
            : this()
        {
            this.backingVector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One);
        }

        /// <summary>
        /// Gets the cyan color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float C
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the magenta color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float M
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

        /// <summary>
        /// Gets the yellow color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float Y
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <summary>
        /// Gets the keyline black color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float K
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.W;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="Cmyk"/> is empty.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsEmpty => this.Equals(Empty);

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Cmyk left, Cmyk right)
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
                return "Cmyk [Empty]";
            }

            return $"Cmyk [ C={this.C:#0.##}, M={this.M:#0.##}, Y={this.Y:#0.##}, K={this.K:#0.##}]";
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            if (obj is Cmyk)
            {
                return this.Equals((Cmyk)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Cmyk other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(Cmyk other, float precision)
        {
            var result = Vector4.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                && result.Y <= precision
                && result.Z <= precision
                && result.W <= precision;
        }
    }
}
// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents an CMYK (cyan, magenta, yellow, keyline) color.
    /// </summary>
    public readonly struct Cmyk : IEquatable<Cmyk>
    {
        private static readonly Vector4 Min = Vector4.Zero;
        private static readonly Vector4 Max = Vector4.One;

        /// <summary>
        /// Gets the cyan color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public readonly float C;

        /// <summary>
        /// Gets the magenta color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public readonly float M;

        /// <summary>
        /// Gets the yellow color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public readonly float Y;

        /// <summary>
        /// Gets the keyline black color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public readonly float K;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cmyk"/> struct.
        /// </summary>
        /// <param name="c">The cyan component.</param>
        /// <param name="m">The magenta component.</param>
        /// <param name="y">The yellow component.</param>
        /// <param name="k">The keyline black component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Cmyk(float c, float m, float y, float k)
            : this(new Vector4(c, m, y, k))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Cmyk"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the c, m, y, k components.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Cmyk(Vector4 vector)
        {
            vector = Numerics.Clamp(vector, Min, Max);
            this.C = vector.X;
            this.M = vector.Y;
            this.Y = vector.Z;
            this.K = vector.W;
        }

        /// <summary>
        /// Compares two <see cref="Cmyk"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Cmyk"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Cmyk"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Cmyk left, Cmyk right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Cmyk"/> objects for inequality.
        /// </summary>
        /// <param name="left">The <see cref="Cmyk"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Cmyk"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Cmyk left, Cmyk right) => !left.Equals(right);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => HashCode.Combine(this.C, this.M, this.Y, this.K);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"Cmyk({this.C:#0.##}, {this.M:#0.##}, {this.Y:#0.##}, {this.K:#0.##})");

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Cmyk other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(Cmyk other)
        {
            return this.C.Equals(other.C)
                && this.M.Equals(other.M)
                && this.Y.Equals(other.Y)
                && this.K.Equals(other.K);
        }
    }
}
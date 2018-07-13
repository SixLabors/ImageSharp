// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents an CMYK (cyan, magenta, yellow, keyline) color.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Cmyk : IEquatable<Cmyk>
    {
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
            vector = Vector4.Clamp(vector, Vector4.Zero, Vector4.One);

            this.C = vector.X;
            this.M = vector.Y;
            this.Y = vector.Z;
            this.K = vector.W;
        }

        /// <summary>
        /// Gets the cyan color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float C { get; }

        /// <summary>
        /// Gets the magenta color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float M { get; }

        /// <summary>
        /// Gets the yellow color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float Y { get; }

        /// <summary>
        /// Gets the keyline black color component.
        /// <remarks>A value ranging between 0 and 1.</remarks>
        /// </summary>
        public float K { get; }

        /// <summary>
        /// Compares two <see cref="Cmyk"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Cmyk"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Cmyk"/> on the right side of the operand.</param>
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
        /// <param name="left">The <see cref="Cmyk"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Cmyk"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Cmyk left, Cmyk right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => (this.C, this.M, this.Y, this.K).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => $"Cmyk({this.C:#0.##},{this.M:#0.##},{this.Y:#0.##},{this.K:#0.##})";

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is Cmyk other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Cmyk other) =>
            this.C.Equals(other.C) &&
            this.M.Equals(other.M) &&
            this.Y.Equals(other.Y) &&
            this.K.Equals(other.K);
    }
}
// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// LMS is a color space represented by the response of the three types of cones of the human eye,
    /// named after their responsivity (sensitivity) at long, medium and short wavelengths.
    /// <see href="https://en.wikipedia.org/wiki/LMS_color_space"/>
    /// </summary>
    public readonly struct Lms : IEquatable<Lms>
    {
        /// <summary>
        /// Gets the L long component.
        /// <remarks>A value usually ranging between -1 and 1.</remarks>
        /// </summary>
        public readonly float L;

        /// <summary>
        /// Gets the M medium component.
        /// <remarks>A value usually ranging between -1 and 1.</remarks>
        /// </summary>
        public readonly float M;

        /// <summary>
        /// Gets the S short component.
        /// <remarks>A value usually ranging between -1 and 1.</remarks>
        /// </summary>
        public readonly float S;

        /// <summary>
        /// Initializes a new instance of the <see cref="Lms"/> struct.
        /// </summary>
        /// <param name="l">L represents the responsivity at long wavelengths.</param>
        /// <param name="m">M represents the responsivity at medium wavelengths.</param>
        /// <param name="s">S represents the responsivity at short wavelengths.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Lms(float l, float m, float s)
            : this(new Vector3(l, m, s))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lms"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the l, m, s components.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Lms(Vector3 vector)
        {
            // Not clamping as documentation about this space only indicates "usual" ranges
            this.L = vector.X;
            this.M = vector.Y;
            this.S = vector.Z;
        }

        /// <summary>
        /// Compares two <see cref="Lms"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Lms"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Lms"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator ==(Lms left, Lms right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="Lms"/> objects for inequality.
        /// </summary>
        /// <param name="left">The <see cref="Lms"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Lms"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(Lms left, Lms right) => !left.Equals(right);

        /// <summary>
        /// Returns a new <see cref="Vector3"/> representing this instance.
        /// </summary>
        /// <returns>The <see cref="Vector3"/>.</returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public Vector3 ToVector3() => new Vector3(this.L, this.M, this.S);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(this.L, this.M, this.S);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"Lms({this.L:#0.##}, {this.M:#0.##}, {this.S:#0.##})");

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is Lms other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(Lms other)
        {
            return this.L.Equals(other.L)
                && this.M.Equals(other.M)
                && this.S.Equals(other.S);
        }
    }
}
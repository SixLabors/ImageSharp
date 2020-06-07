// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents an YCbCr (luminance, blue chroma, red chroma) color as defined in the ITU-T T.871 specification for the JFIF use with Jpeg.
    /// <see href="http://en.wikipedia.org/wiki/YCbCr"/>
    /// <see href="http://www.ijg.org/files/T-REC-T.871-201105-I!!PDF-E.pdf"/>
    /// </summary>
    public readonly struct YCbCr : IEquatable<YCbCr>
    {
        private static readonly Vector3 Min = Vector3.Zero;
        private static readonly Vector3 Max = new Vector3(255);

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public readonly float Y;

        /// <summary>
        /// Gets the Cb chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public readonly float Cb;

        /// <summary>
        /// Gets the Cr chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public readonly float Cr;

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCr"/> struct.
        /// </summary>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public YCbCr(float y, float cb, float cr)
            : this(new Vector3(y, cb, cr))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCr"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the y, cb, cr components.</param>
        [MethodImpl(InliningOptions.ShortMethod)]
        public YCbCr(Vector3 vector)
        {
            vector = Vector3.Clamp(vector, Min, Max);
            this.Y = vector.X;
            this.Cb = vector.Y;
            this.Cr = vector.Z;
        }

        /// <summary>
        /// Compares two <see cref="YCbCr"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="YCbCr"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="YCbCr"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(YCbCr left, YCbCr right) => left.Equals(right);

        /// <summary>
        /// Compares two <see cref="YCbCr"/> objects for inequality.
        /// </summary>
        /// <param name="left">The <see cref="YCbCr"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="YCbCr"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static bool operator !=(YCbCr left, YCbCr right) => !left.Equals(right);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public override int GetHashCode() => HashCode.Combine(this.Y, this.Cb, this.Cr);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant($"YCbCr({this.Y}, {this.Cb}, {this.Cr})");

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is YCbCr other && this.Equals(other);

        /// <inheritdoc/>
        [MethodImpl(InliningOptions.ShortMethod)]
        public bool Equals(YCbCr other)
        {
            return this.Y.Equals(other.Y)
                && this.Cb.Equals(other.Cb)
                && this.Cr.Equals(other.Cr);
        }
    }
}
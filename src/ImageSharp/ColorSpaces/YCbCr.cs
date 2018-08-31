// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.ColorSpaces
{
    /// <summary>
    /// Represents an YCbCr (luminance, blue chroma, red chroma) color as defined in the ITU-T T.871 specification for the JFIF use with Jpeg.
    /// <see href="http://en.wikipedia.org/wiki/YCbCr"/>
    /// <see href="http://www.ijg.org/files/T-REC-T.871-201105-I!!PDF-E.pdf"/>
    /// </summary>
    internal readonly struct YCbCr : IColorVector, IEquatable<YCbCr>, IAlmostEquatable<YCbCr, float>
    {
        /// <summary>
        /// Vector which is used in clamping to the max value.
        /// </summary>
        private static readonly Vector3 VectorMax = new Vector3(255F);

        /// <summary>
        /// The backing vector for SIMD support.
        /// </summary>
        private readonly Vector3 backingVector;

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCr"/> struct.
        /// </summary>
        /// <param name="y">The y luminance component.</param>
        /// <param name="cb">The cb chroma component.</param>
        /// <param name="cr">The cr chroma component.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public YCbCr(float y, float cb, float cr)
            : this(new Vector3(y, cb, cr))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="YCbCr"/> struct.
        /// </summary>
        /// <param name="vector">The vector representing the y, cb, cr components.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public YCbCr(Vector3 vector)
        {
            this.backingVector = Vector3.Clamp(vector, Vector3.Zero, VectorMax);
        }

        /// <summary>
        /// Gets the Y luminance component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Y
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.X;
        }

        /// <summary>
        /// Gets the Cb chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Cb
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Y;
        }

        /// <summary>
        /// Gets the Cr chroma component.
        /// <remarks>A value ranging between 0 and 255.</remarks>
        /// </summary>
        public float Cr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.backingVector.Z;
        }

        /// <inheritdoc/>
        public Vector3 Vector => this.backingVector;

        /// <summary>
        /// Compares two <see cref="YCbCr"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="YCbCr"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="YCbCr"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        public static bool operator ==(YCbCr left, YCbCr right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="YCbCr"/> objects for inequality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="YCbCr"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="YCbCr"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(YCbCr left, YCbCr right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return this.backingVector.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.Equals(default)
                ? "YCbCr [ Empty ]"
                : $"YCbCr [ Y={this.Y}, Cb={this.Cb}, Cr={this.Cr} ]";
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is YCbCr other && this.Equals(other);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(YCbCr other)
        {
            return this.backingVector.Equals(other.backingVector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AlmostEquals(YCbCr other, float precision)
        {
            var result = Vector3.Abs(this.backingVector - other.backingVector);

            return result.X <= precision
                   && result.Y <= precision
                   && result.Z <= precision;
        }
    }
}
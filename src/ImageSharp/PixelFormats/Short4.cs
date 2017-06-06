// <copyright file="Short4.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.PixelFormats
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Packed pixel type containing four 16-bit signed integer values.
    /// <para>
    /// Ranges from &lt;-37267, -37267, -37267, -37267&gt; to &lt;37267, 37267, 37267, 37267&gt; in vector form.
    /// </para>
    /// </summary>
    public struct Short4 : IPixel<Short4>, IPackedVector<ulong>
    {
        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half the maximum byte value.
        /// </summary>
        private static readonly Vector4 Half = new Vector4(127);

        /// <summary>
        /// The vector value used for rounding.
        /// </summary>
        private static readonly Vector4 Round = new Vector4(.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="Short4"/> struct.
        /// </summary>
        /// <param name="vector">A vector containing the initial values for the components.</param>
        public Short4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Short4"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        public Short4(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <inheritdoc/>
        public ulong PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Short4"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Short4"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Short4"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Short4 left, Short4 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Short4"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Short4"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Short4"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Short4 left, Short4 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public PixelOperations<Short4> CreatePixelOperations() => new PixelOperations<Short4>();

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(
                (short)(this.PackedValue & 0xFFFF),
                (short)((this.PackedValue >> 0x10) & 0xFFFF),
                (short)((this.PackedValue >> 0x20) & 0xFFFF),
                (short)((this.PackedValue >> 0x30) & 0xFFFF));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            Vector4 vector = source.ToVector4();
            vector *= 65534;
            vector -= new Vector4(32767);
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            Vector4 vector = this.ToScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is Short4) && this == (Short4)obj;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Short4 other)
        {
            return this == other;
        }

        /// <summary>
        /// Gets the hash code for the current instance.
        /// </summary>
        /// <returns>Hash code for the instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <summary>
        /// Returns a string representation of the current instance.
        /// </summary>
        /// <returns>String that represents the object.</returns>
        public override string ToString()
        {
            return this.PackedValue.ToString("x16");
        }

        /// <summary>
        /// Packs the components into a <see cref="ulong"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="ulong"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Pack(float x, float y, float z, float w)
        {
            // Largest two byte positive number 0xFFFF >> 1;
            const float MaxPos = 0x7FFF;

            // Two's complement
            const float MinNeg = ~(int)MaxPos;

            // Clamp the value between min and max values
            ulong word4 = ((ulong)Math.Round(x.Clamp(MinNeg, MaxPos)) & 0xFFFF) << 0x00;
            ulong word3 = ((ulong)Math.Round(y.Clamp(MinNeg, MaxPos)) & 0xFFFF) << 0x10;
            ulong word2 = ((ulong)Math.Round(z.Clamp(MinNeg, MaxPos)) & 0xFFFF) << 0x20;
            ulong word1 = ((ulong)Math.Round(w.Clamp(MinNeg, MaxPos)) & 0xFFFF) << 0x30;

            return word4 | word3 | word2 | word1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 ToScaledVector4()
        {
            Vector4 vector = this.ToVector4();
            vector /= 65534;
            vector *= 255;
            vector += Half;
            vector += Round;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);
            return vector;
        }
    }
}
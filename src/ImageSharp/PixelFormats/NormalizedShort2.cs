﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing two 16-bit signed normalized values, ranging from −1 to 1.
    /// <para>
    /// Ranges from [-1, -1, 0, 1] to [1, 1, 0, 1] in vector form.
    /// </para>
    /// </summary>
    public struct NormalizedShort2 : IPixel<NormalizedShort2>, IPackedVector<uint>
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
        /// Initializes a new instance of the <see cref="NormalizedShort2"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public NormalizedShort2(Vector2 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizedShort2"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        public NormalizedShort2(float x, float y)
        {
            this.PackedValue = Pack(x, y);
        }

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="NormalizedShort2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedShort2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedShort2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(NormalizedShort2 left, NormalizedShort2 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="NormalizedShort2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedShort2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedShort2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(NormalizedShort2 left, NormalizedShort2 right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public PixelOperations<NormalizedShort2> CreatePixelOperations() => new PixelOperations<NormalizedShort2>();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromScaledVector4(Vector4 vector)
        {
            Vector2 scaled = new Vector2(vector.X, vector.Y) * 2F;
            scaled -= Vector2.One;
            this.PackedValue = Pack(scaled.X, scaled.Y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToScaledVector4()
        {
            var scaled = this.ToVector2();
            scaled += Vector2.One;
            scaled /= 2F;
            return new Vector4(scaled, 0F, 1F);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(this.ToVector2(), 0, 1);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            Vector4 vector = source.ToByteScaledVector4();
            vector -= Round;
            vector -= Half;
            vector -= Round;
            vector /= Half;
            this.PackFromVector4(vector);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            Vector4 vector = source.ToByteScaledVector4();
            vector -= Round;
            vector -= Half;
            vector -= Round;
            vector /= Half;
            this.PackFromVector4(vector);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            Vector4 vector = source.ToByteScaledVector4();
            vector -= Round;
            vector -= Half;
            vector -= Round;
            vector /= Half;
            this.PackFromVector4(vector);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
            dest.A = 255;
        }

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector2"/>.
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector2"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ToVector2()
        {
            const float MaxVal = 0x7FFF;

            return new Vector2(
                (short)(this.PackedValue & 0xFFFF) / MaxVal,
                (short)(this.PackedValue >> 0x10) / MaxVal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return (obj is NormalizedShort2) && this.Equals((NormalizedShort2)obj);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NormalizedShort2 other)
        {
            return this.PackedValue.Equals(other.PackedValue);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return this.PackedValue.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.PackedValue.ToString("X");
        }

        /// <summary>
        /// Packs the <see cref="float"/> components into a <see cref="uint"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <returns>The <see cref="uint"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Pack(float x, float y)
        {
            const float MaxPos = 0x7FFF;
            const float MinNeg = -MaxPos;

            // Clamp the value between min and max values
            // Round rather than truncate.
            uint word2 = (uint)((int)MathF.Round(x * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF);
            uint word1 = (uint)(((int)MathF.Round(y * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x10);

            return word2 | word1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 ToByteScaledVector4()
        {
            var vector = this.ToVector4();
            vector *= Half;
            vector += Round;
            vector += Half;
            vector += Round;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);
            return vector;
        }
    }
}
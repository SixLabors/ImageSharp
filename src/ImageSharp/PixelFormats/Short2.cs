// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing two 16-bit signed integer values.
    /// <para>
    /// Ranges from [-32767, -32767, 0, 1] to [32767, 32767, 0, 1] in vector form.
    /// </para>
    /// </summary>
    public struct Short2 : IPixel<Short2>, IPackedVector<uint>
    {
        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector2 MaxBytes = new Vector2(255);

        /// <summary>
        /// The half the maximum byte value.
        /// </summary>
        private static readonly Vector2 Half = new Vector2(127);

        /// <summary>
        /// The vector value used for rounding.
        /// </summary>
        private static readonly Vector2 Round = new Vector2(.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="Short2"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public Short2(Vector2 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Short2"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        public Short2(float x, float y)
        {
            this.PackedValue = Pack(x, y);
        }

        /// <inheritdoc/>
        public uint PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Short2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Short2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Short2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Short2 left, Short2 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Short2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Short2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Short2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Short2 left, Short2 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public PixelOperations<Short2> CreatePixelOperations() => new PixelOperations<Short2>();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromScaledVector4(Vector4 vector)
        {
            Vector2 scaled = new Vector2(vector.X, vector.Y) * 65534F;
            scaled -= new Vector2(32767F);
            this.PackedValue = Pack(scaled.X, scaled.Y);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToScaledVector4()
        {
            var scaled = this.ToVector2();
            scaled += new Vector2(32767F);
            scaled /= 65534F;
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
            return new Vector4((short)(this.PackedValue & 0xFFFF), (short)(this.PackedValue >> 0x10), 0, 1);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            Vector2 vector = new Vector2(source.R, source.G) / 255;
            vector *= 65534;
            vector -= new Vector2(32767);
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            Vector2 vector = new Vector2(source.R, source.G) / 255;
            vector *= 65534;
            vector -= new Vector2(32767);
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            Vector2 vector = new Vector2(source.R, source.G) / 255;
            vector *= 65534;
            vector -= new Vector2(32767);
            this.PackedValue = Pack(vector.X, vector.Y);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            Vector2 vector = this.ToByteScaledVector2();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector2 vector = this.ToByteScaledVector2();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            Vector2 vector = this.ToByteScaledVector2();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector2 vector = this.ToByteScaledVector2();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector2 vector = this.ToByteScaledVector2();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = 0;
            dest.A = 255;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgb48(Rgb48 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb48(ref Rgb48 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba64(Rgba64 source) => this.PackFromScaledVector4(source.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba64(ref Rgba64 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <summary>
        /// Expands the packed representation into a <see cref="Vector2"/>.
        /// The vector components are typically expanded in least to greatest significance order.
        /// </summary>
        /// <returns>The <see cref="Vector2"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 ToVector2()
        {
            return new Vector2((short)(this.PackedValue & 0xFFFF), (short)(this.PackedValue >> 0x10));
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Short2 other && this.Equals(other);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Short2 other)
        {
            return this == other;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();

        /// <inheritdoc />
        public override string ToString()
        {
            return this.PackedValue.ToString("x8");
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
            // Largest two byte positive number 0xFFFF >> 1;
            const float MaxPos = 0x7FFF;
            const float MinNeg = ~(int)MaxPos;

            // Clamp the value between min and max values
            uint word2 = (uint)Math.Round(x.Clamp(MinNeg, MaxPos)) & 0xFFFF;
            uint word1 = ((uint)Math.Round(y.Clamp(MinNeg, MaxPos)) & 0xFFFF) << 0x10;

            return word2 | word1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector2 ToByteScaledVector2()
        {
            var vector = this.ToVector2();
            vector /= 65534;
            vector *= 255;
            vector += Half;
            vector += Round;
            vector = Vector2.Clamp(vector, Vector2.Zero, MaxBytes);
            return vector;
        }
    }
}
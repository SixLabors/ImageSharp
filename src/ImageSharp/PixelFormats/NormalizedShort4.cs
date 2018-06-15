// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 16-bit signed normalized values, ranging from −1 to 1.
    /// <para>
    /// Ranges from [-1, -1, -1, -1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public struct NormalizedShort4 : IPixel<NormalizedShort4>, IPackedVector<ulong>
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
        /// Initializes a new instance of the <see cref="NormalizedShort4"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the component values.</param>
        public NormalizedShort4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NormalizedShort4"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        public NormalizedShort4(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <inheritdoc/>
        public ulong PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="NormalizedShort4"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedShort4"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedShort4"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(NormalizedShort4 left, NormalizedShort4 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="NormalizedShort4"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="NormalizedShort4"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="NormalizedShort4"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(NormalizedShort4 left, NormalizedShort4 right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public PixelOperations<NormalizedShort4> CreatePixelOperations() => new PixelOperations<NormalizedShort4>();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromScaledVector4(Vector4 vector)
        {
            vector *= 2F;
            vector -= Vector4.One;
            this.PackFromVector4(vector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToScaledVector4()
        {
            var scaled = this.ToVector4();
            scaled += Vector4.One;
            scaled /= 2F;
            return scaled;
        }

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
            const float MaxVal = 0x7FFF;

            return new Vector4(
                         (short)((this.PackedValue >> 0x00) & 0xFFFF) / MaxVal,
                         (short)((this.PackedValue >> 0x10) & 0xFFFF) / MaxVal,
                         (short)((this.PackedValue >> 0x20) & 0xFFFF) / MaxVal,
                         (short)((this.PackedValue >> 0x30) & 0xFFFF) / MaxVal);
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
            dest.B = (byte)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)MathF.Round(vector.X);
            dest.G = (byte)MathF.Round(vector.Y);
            dest.B = (byte)MathF.Round(vector.Z);
            dest.A = (byte)MathF.Round(vector.W);
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

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is NormalizedShort4 other && this.Equals(other);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NormalizedShort4 other)
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
        /// Packs the <see cref="float"/> components into a <see cref="ulong"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="ulong"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Pack(float x, float y, float z, float w)
        {
            const float MaxPos = 0x7FFF;
            const float MinNeg = -MaxPos;

            // Clamp the value between min and max values
            ulong word4 = ((ulong)MathF.Round(x * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x00;
            ulong word3 = ((ulong)MathF.Round(y * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x10;
            ulong word2 = ((ulong)MathF.Round(z * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x20;
            ulong word1 = ((ulong)MathF.Round(w * MaxPos).Clamp(MinNeg, MaxPos) & 0xFFFF) << 0x30;

            return word4 | word3 | word2 | word1;
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

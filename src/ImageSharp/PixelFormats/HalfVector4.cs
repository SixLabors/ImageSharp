// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing four 16-bit floating-point values.
    /// <para>
    /// Ranges from [-1, -1, -1, -1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public struct HalfVector4 : IPixel<HalfVector4>, IPackedVector<ulong>
    {
        /// <summary>
        /// The maximum byte value.
        /// </summary>
        private static readonly Vector4 MaxBytes = new Vector4(255);

        /// <summary>
        /// The half vector value.
        /// </summary>
        private static readonly Vector4 Half = new Vector4(0.5F);

        /// <summary>
        /// Initializes a new instance of the <see cref="HalfVector4"/> struct.
        /// </summary>
        /// <param name="x">The x-component.</param>
        /// <param name="y">The y-component.</param>
        /// <param name="z">The z-component.</param>
        /// <param name="w">The w-component.</param>
        public HalfVector4(float x, float y, float z, float w)
        {
            var vector = new Vector4(x, y, z, w);
            this.PackedValue = Pack(ref vector);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HalfVector4"/> struct.
        /// </summary>
        /// <param name="vector">A vector containing the initial values for the components</param>
        public HalfVector4(Vector4 vector)
        {
            this.PackedValue = Pack(ref vector);
        }

        /// <inheritdoc/>
        public ulong PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="HalfVector2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="HalfVector2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="HalfVector2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(HalfVector4 left, HalfVector4 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="HalfVector2"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="HalfVector2"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="HalfVector2"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(HalfVector4 left, HalfVector4 right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc />
        public PixelOperations<HalfVector4> CreatePixelOperations() => new PixelOperations<HalfVector4>();

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
            this.PackedValue = Pack(ref vector);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(
                HalfTypeHelper.Unpack((ushort)this.PackedValue),
                HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x10)),
                HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x20)),
                HalfTypeHelper.Unpack((ushort)(this.PackedValue >> 0x30)));
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.PackFromVector4(source.ToVector4());
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            this.PackFromVector4(source.ToVector4());
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.PackFromVector4(source.ToVector4());
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
            dest.A = (byte)vector.W;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
            dest.A = (byte)vector.W;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToByteScaledVector4();
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
            dest.A = (byte)vector.W;
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
        public override string ToString()
        {
            return this.ToVector4().ToString();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is HalfVector4 other && this.Equals(other);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(HalfVector4 other)
        {
            return this.PackedValue.Equals(other.PackedValue);
        }

        /// <summary>
        /// Packs a <see cref="Vector4"/> into a <see cref="ulong"/>.
        /// </summary>
        /// <param name="vector">The vector containing the values to pack.</param>
        /// <returns>The <see cref="ulong"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Pack(ref Vector4 vector)
        {
            ulong num4 = HalfTypeHelper.Pack(vector.X);
            ulong num3 = (ulong)HalfTypeHelper.Pack(vector.Y) << 0x10;
            ulong num2 = (ulong)HalfTypeHelper.Pack(vector.Z) << 0x20;
            ulong num1 = (ulong)HalfTypeHelper.Pack(vector.W) << 0x30;
            return num4 | num3 | num2 | num1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector4 ToByteScaledVector4()
        {
            var vector = this.ToVector4();
            vector *= MaxBytes;
            vector += Half;
            vector = Vector4.Clamp(vector, Vector4.Zero, MaxBytes);
            return vector;
        }
    }
}
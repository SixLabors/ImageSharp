// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing unsigned normalized values, ranging from 0 to 1, using 4 bits each for x, y, z, and w.
    /// <para>
    /// Ranges from [0, 0, 0, 0] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public struct Bgra4444 : IPixel<Bgra4444>, IPackedVector<ushort>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra4444"/> struct.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        public Bgra4444(float x, float y, float z, float w)
        {
            this.PackedValue = Pack(x, y, z, w);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Bgra4444"/> struct.
        /// </summary>
        /// <param name="vector">The vector containing the components for the packed vector.</param>
        public Bgra4444(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
        }

        /// <inheritdoc/>
        public ushort PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Bgra4444"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgra4444"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgra4444"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Bgra4444 left, Bgra4444 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Bgra4444"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Bgra4444"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Bgra4444"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Bgra4444 left, Bgra4444 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public PixelOperations<Bgra4444> CreatePixelOperations() => new PixelOperations<Bgra4444>();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromScaledVector4(Vector4 vector)
        {
            this.PackFromVector4(vector);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToScaledVector4()
        {
            return this.ToVector4();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            const float Max = 1 / 15F;

            return new Vector4(
                ((this.PackedValue >> 8) & 0x0F) * Max,
                ((this.PackedValue >> 4) & 0x0F) * Max,
                (this.PackedValue & 0x0F) * Max,
                ((this.PackedValue >> 12) & 0x0F) * Max);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z, vector.W);
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
            Vector4 vector = this.ToVector4() * 255F;
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            Vector4 vector = this.ToVector4() * 255F;
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
            dest.A = (byte)vector.W;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            Vector4 vector = this.ToVector4() * 255F;
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
            dest.A = (byte)vector.W;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            Vector4 vector = this.ToVector4() * 255F;
            dest.R = (byte)vector.X;
            dest.G = (byte)vector.Y;
            dest.B = (byte)vector.Z;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            Vector4 vector = this.ToVector4() * 255F;
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
        public override bool Equals(object obj)
        {
            return obj is Bgra4444 other && this.Equals(other);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Bgra4444 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.ToVector4().ToString();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();

        /// <summary>
        /// Packs the <see cref="float"/> components into a <see cref="ushort"/>.
        /// </summary>
        /// <param name="x">The x-component</param>
        /// <param name="y">The y-component</param>
        /// <param name="z">The z-component</param>
        /// <param name="w">The w-component</param>
        /// <returns>The <see cref="ushort"/> containing the packed values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort Pack(float x, float y, float z, float w)
        {
            return (ushort)((((int)Math.Round(w.Clamp(0, 1) * 15F) & 0x0F) << 12) |
                            (((int)Math.Round(x.Clamp(0, 1) * 15F) & 0x0F) << 8) |
                            (((int)Math.Round(y.Clamp(0, 1) * 15F) & 0x0F) << 4) |
                            ((int)Math.Round(z.Clamp(0, 1) * 15F) & 0x0F));
        }
    }
}

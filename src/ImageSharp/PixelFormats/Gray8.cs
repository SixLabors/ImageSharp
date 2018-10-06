// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.PixelFormats
{
    /// <summary>
    /// Packed pixel type containing a single 8 bit normalized gray values.
    /// <para>
    /// Ranges from [0, 0, 0, 1] to [1, 1, 1, 1] in vector form.
    /// </para>
    /// </summary>
    public struct Gray8 : IPixel<Gray8>, IPackedVector<byte>
    {
        /// <summary>
        /// RX as in ITU-R recommendation 709 to match libpng
        /// </summary>
        private const float Rx = .2126F;

        /// <summary>
        /// GX as in ITU-R recommendation 709 to match libpng
        /// </summary>
        private const float Gx = .7152F;

        /// <summary>
        /// BX as in ITU-R recommendation 709 to match libpng
        /// </summary>
        private const float Bx = .0722F;

        /// <summary>
        /// Initializes a new instance of the <see cref="Gray8"/> struct.
        /// </summary>
        /// <param name="gray">The gray component</param>
        public Gray8(byte gray)
        {
            this.PackedValue = gray;
        }

        /// <inheritdoc />
        public byte PackedValue { get; set; }

        /// <summary>
        /// Compares two <see cref="Gray8"/> objects for equality.
        /// </summary>
        /// <param name="left">
        /// The <see cref="Gray8"/> on the left side of the operand.
        /// </param>
        /// <param name="right">
        /// The <see cref="Gray8"/> on the right side of the operand.
        /// </param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Gray8 left, Gray8 right)
        {
            return left.PackedValue == right.PackedValue;
        }

        /// <summary>
        /// Compares two <see cref="Gray8"/> objects for equality.
        /// </summary>
        /// <param name="left">The <see cref="Gray8"/> on the left side of the operand.</param>
        /// <param name="right">The <see cref="Gray8"/> on the right side of the operand.</param>
        /// <returns>
        /// True if the <paramref name="left"/> parameter is not equal to the <paramref name="right"/> parameter; otherwise, false.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Gray8 left, Gray8 right)
        {
            return left.PackedValue != right.PackedValue;
        }

        /// <inheritdoc />
        public PixelOperations<Gray8> CreatePixelOperations() => new PixelOperations<Gray8>();

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
            var scaledGray = this.PackedValue / 255f;
            return new Vector4(scaledGray, scaledGray, scaledGray, 1.0f);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromVector4(Vector4 vector)
        {
            this.PackedValue = Pack(vector.X, vector.Y, vector.Z);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return new Vector4(this.PackedValue, this.PackedValue, this.PackedValue, 1.0f);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba32(Rgba32 source)
        {
            this.PackedValue = Pack(source.R, source.G, source.B);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromArgb32(Argb32 source)
        {
            this.PackedValue = Pack(source.R, source.G, source.B);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromBgra32(Bgra32 source)
        {
            this.PackedValue = Pack(source.R, source.G, source.B);
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb24(ref Rgb24 dest)
        {
            dest.R = this.PackedValue;
            dest.G = this.PackedValue;
            dest.B = this.PackedValue;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba32(ref Rgba32 dest)
        {
            dest.R = this.PackedValue;
            dest.G = this.PackedValue;
            dest.B = this.PackedValue;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToArgb32(ref Argb32 dest)
        {
            dest.R = this.PackedValue;
            dest.G = this.PackedValue;
            dest.B = this.PackedValue;
            dest.A = 255;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgr24(ref Bgr24 dest)
        {
            dest.R = this.PackedValue;
            dest.G = this.PackedValue;
            dest.B = this.PackedValue;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToBgra32(ref Bgra32 dest)
        {
            dest.R = this.PackedValue;
            dest.G = this.PackedValue;
            dest.B = this.PackedValue;
            dest.A = 255;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgb48(Rgb48 source) =>
            this.PackedValue = Pack(source.R, source.G, source.B);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgb48(ref Rgb48 dest)
        {
            ushort gray = (ushort)(this.PackedValue * 255);
            dest.R = gray;
            dest.G = gray;
            dest.B = gray;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromRgba64(Rgba64 source) =>
            this.PackFromScaledVector4(source.ToScaledVector4());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromGray8(Gray8 source) => this.PackedValue = source.PackedValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PackFromGray16(Gray16 source) => this.PackedValue = (byte)(source.PackedValue / 255);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToRgba64(ref Rgba64 dest) => dest.PackFromScaledVector4(this.ToScaledVector4());

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToGray8(ref Gray8 dest) => dest.PackedValue = this.PackedValue;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ToGray16(ref Gray16 dest) => dest.PackedValue = (ushort)(this.PackedValue * 255);

        /// <summary>
        /// Compares an object with the packed vector.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if the object is equal to the packed vector.</returns>
        public override bool Equals(object obj)
        {
            return obj is Gray8 other && this.Equals(other);
        }

        /// <summary>
        /// Compares another <see cref="Gray8" /> packed vector with the packed vector.
        /// </summary>
        /// <param name="other">The Gray8 packed vector to compare.</param>
        /// <returns>True if the packed vectors are equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Gray8 other)
        {
            return this.PackedValue == other.PackedValue;
        }

        /// <summary>
        /// Gets a string representation of the packed vector.
        /// </summary>
        /// <returns>A string representation of the packed vector.</returns>
        public override string ToString()
        {
            return (this.PackedValue / 255F).ToString();
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => this.PackedValue.GetHashCode();

        /// <summary>
        /// Packs a <see cref="float"/> into a byte.
        /// </summary>
        /// <param name="r">Red value of the color to pack.</param>
        /// <param name="g">Green value of the color to pack.</param>
        /// <param name="b">Blue value of the color to pack.</param>
        /// <returns>The <see cref="byte"/> containing the packed value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte Pack(float r, float g, float b)
        {
            float val = (r * Rx) + (g * Gx) + (b * Bx);
            return (byte)Math.Round(val * 255);
        }
    }
}